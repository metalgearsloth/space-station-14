using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.GameObjects.Components.Doors;
using Content.Server.GameObjects.EntitySystems.Pathfinding.Pathfinders;
using Content.Server.GameObjects.EntitySystems.Pathfinding.Updates;
using Robust.Server.GameObjects;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Map;

namespace Content.Server.GameObjects.EntitySystems.Pathfinding
{
    /*
    TODO:
    Need to store node neighbors
    What I should do is call an RefreshNodeNeighbors(List<PathfindingNode> nodes) on the chunk
    Also add a thing that tells what edge something is on and then it will also tell those region(s)
    to refresh their neighbors and what side is telling them to do so

    // TODO: Need to fix multiple collision layers and also cache each tile's neighbors. Maybe on every call to update a node check which edge(s) it's on then calling updateneighbor on theirs (and set a flag for false so it's not recursive)
    // TODO: IMO use rectangular symmetry reduction on the nodes with collision at all.
    // TODO: Look at storing different graphs for each mask
    */
    public class PathfindingSystem : EntitySystem
    {

        [Dependency] private readonly IMapManager _mapManager;
        [Dependency] private readonly IPathfinder _pathfinder;

        // Pathfinding chunks stored by row
        private readonly Dictionary<GridId, List<PathfindingChunk>> _graph = new Dictionary<GridId, List<PathfindingChunk>>();

        // Queues
        private Queue<Task> _runningPathfinders = new Queue<Task>();
        private Queue<Task> _queuedPathfinders = new Queue<Task>();
        // Every frame we queue up all the changes and do them at once
        private Task _updateTask;
        private Queue<IPathfindingGraphUpdate> _runningUpdates = new Queue<IPathfindingGraphUpdate>();
        private Queue<IPathfindingGraphUpdate> _queuedUpdatesSync = new Queue<IPathfindingGraphUpdate>();
        private Queue<IPathfindingGraphUpdate> _queuedUpdates = new Queue<IPathfindingGraphUpdate>();

        // Need to store previously known entity positions for collidables for when they move
        private Dictionary<IEntity, TileRef> _lastKnownPositions = new Dictionary<IEntity, TileRef>();

        private float _lastUpdate;
        private const float MaxUpdateTime = 1.0f;

        public Task<Queue<TileRef>> RequestPath(PathfindingArgs pathfindingArgs, CancellationTokenSource cancelToken = null)
        {
            if (cancelToken == null)
            {
                cancelToken = new CancellationTokenSource();
            }

            if (!_graph.TryGetValue(pathfindingArgs.Start.GridIndex, out var chunks))
            {
                return null;
            }
            var result = new Task<Queue<TileRef>>(() => _pathfinder.FindPath(chunks, pathfindingArgs), cancelToken.Token);
            _queuedPathfinders.Enqueue(result);
            return result;
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            _lastUpdate += frameTime;

            var newRunningPathfinders = new Queue<Task>();

            // Handle all the pathfinders that have finished.
            foreach (var pathfinderTask in _runningPathfinders)
            {
                switch (pathfinderTask.Status)
                {
                    case TaskStatus.RanToCompletion:
                        break;
                    default:
                        newRunningPathfinders.Enqueue(pathfinderTask);
                        break;
                }
            }

            _runningPathfinders.Clear();
            _runningPathfinders = new Queue<Task>(newRunningPathfinders);

            if (_queuedPathfinders.Count > 0)
            {
                _updateTask?.Wait();
                _updateTask = null;

                foreach (var queued in _queuedPathfinders)
                {
                    queued.Start();
                    _runningPathfinders.Enqueue(queued);
                }

                _queuedPathfinders.Clear();

                _queuedPathfinders.Clear();
            }

            if ((_queuedUpdates.Count > 0 || _queuedUpdatesSync.Count > 0) && (_runningPathfinders.Count == 0 || _lastUpdate > MaxUpdateTime))
            {
                // TODO: Probably have a function for handling completed pathfinders
                _lastUpdate = 0.0f;

                // Finish up previous tasks
                foreach (var task in _runningPathfinders)
                {
                    task.Wait();
                }
                _updateTask?.Wait();
                _updateTask = null;

                // Queue new updates, first synchronous ones (e.g. adding / removing entities) then the task ones
                _runningPathfinders.Clear();
                ProcessUpdatesSync();
                _queuedUpdatesSync.Clear();
                _runningUpdates = new Queue<IPathfindingGraphUpdate>(_queuedUpdates);
                _queuedUpdates.Clear();
                _updateTask = Task.Run(ProcessUpdates);
            }
        }

        private void ProcessUpdatesSync()
        {
            foreach (var update in _queuedUpdatesSync)
            {
                switch (update)
                {
                    case CollisionChange change:
                        if (change.Value)
                        {
                            HandleCollidableAdd(change.Owner);
                        }
                        else
                        {
                            HandleCollidableRemove(change.Owner);
                        }

                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        #region UpdateHandlers
        /// <summary>
        /// Runs through all the queued updates and passes them to the appropriate function handler
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        private void ProcessUpdates()
        {
            foreach (var update in _runningUpdates)
            {
                switch (update)
                {
                    case CollidableMove move:
                        HandleCollidableMove(move);
                        break;
                    case GridRemoval remove:
                        RemovePathfindingGrid(remove);
                        break;
                    case TileUpdate tileUpdate:
                        UpdateNodeTile(tileUpdate);
                        break;
                    default:
                        // TODO: Log not implemented
                        throw new ArgumentOutOfRangeException();
                }
            }

            _runningUpdates.Clear();
        }

        private void QueueCollidableMove(object sender, MoveEventArgs eventArgs, IEntity entity)
        {
            var oldTile = _lastKnownPositions[entity];
            var newTile = _mapManager.GetGrid(eventArgs.NewPosition.GridID).GetTileRef(eventArgs.NewPosition);

            if (oldTile == newTile)
            {
                return;
            }

            var collisionLayer = entity.GetComponent<CollidableComponent>().CollisionLayer;

            _queuedUpdates.Enqueue(new CollidableMove(collisionLayer, oldTile, newTile));
        }

        // TODO: Creating Chunks should be synchronous on the main thread
        private void HandleCollidableMove(CollidableMove move)
        {
            var gridIds = new HashSet<GridId>(2) {move.OldTile.GridIndex, move.NewTile.GridIndex};
            var oldUpdated = false;
            var newUpdated = false;
            foreach (var gridId in gridIds)
            {
                if (oldUpdated && newUpdated) break;

                foreach (var chunk in GetChunks(gridId))
                {
                    if (oldUpdated && newUpdated) break;

                    if (!oldUpdated && chunk.TryGetNode(move.OldTile, out var oldNode))
                    {
                        oldNode.RemoveCollisionLayer(move.CollisionLayer);
                        oldUpdated = true;
                    }
                    if (!newUpdated && chunk.TryGetNode(move.NewTile, out var newNode))
                    {
                        newNode.AddCollisionLayer(move.CollisionLayer);
                        newNode.UpdateTile(move.NewTile);
                        newUpdated = true;
                    }
                }
            }

        }

        private void RemovePathfindingGrid(GridRemoval removal)
        {
            if (!_graph.ContainsKey(removal.GridId))
            {
                throw new InvalidOperationException();
            }

            _graph.Remove(removal.GridId);
        }

        private void UpdateNodeTile(TileUpdate tile)
        {
            foreach (var chunk in GetChunks(tile.Tile.GridIndex))
            {
                if (chunk.TryUpdateNode(tile.Tile))
                {
                    return;
                }
            }
        }
        #endregion

        public List<PathfindingChunk> GetChunks(GridId gridId)
        {
            if (_graph.ContainsKey(gridId))
            {
                return _graph[gridId];
            }

            var grid = _mapManager.GetGrid(gridId);

            foreach (var tile in grid.GetAllTiles())
            {
                GetChunk(tile);
            }

            _graph.TryGetValue(gridId, out var chunks);

            return chunks ?? new List<PathfindingChunk>();
        }

        public PathfindingChunk GetChunk(TileRef tile)
        {
            if (_graph.TryGetValue(tile.GridIndex, out var chunks))
            {
                foreach (var chunk in chunks)
                {
                    if (chunk.InBounds(tile))
                    {
                        return chunk;
                    }
                }
            }

            var chunkX = (int) (Math.Floor((float) tile.X / PathfindingChunk.ChunkSize) * PathfindingChunk.ChunkSize);
            var chunkY = (int) (Math.Floor((float) tile.Y / PathfindingChunk.ChunkSize) * PathfindingChunk.ChunkSize);

            var newChunk = CreateChunk(tile.GridIndex, new MapIndices(chunkX, chunkY));
            // TODO: Refresh neighbors on all tiles; all the interior nodes have interior neighbors but all the ones on edges need to notify edge chunks to update
            return newChunk;
        }

        private PathfindingChunk CreateChunk(GridId gridId, MapIndices indices)
        {
            var newChunk = new PathfindingChunk(gridId, indices);
            newChunk.Initialize();
            // Need to add pathfinding node neighbor references
            // TODO: Each neighbor needs to store its direction. Make a dedicated function to alert neighbors to update specific tiles
            if (_graph.TryGetValue(gridId, out var chunks))
            {
                foreach (var chunk in chunks)
                {
                    if (newChunk.AreNeighbors(chunk))
                    {
                        newChunk.AddNeighbor(chunk);
                    }
                }
            }
            else
            {
                _graph.Add(gridId, new List<PathfindingChunk>());
            }

            _graph[gridId].Add(newChunk);
            Logger.DebugS("pathfinding", $"Created chunk at grid {gridId} : {indices}");
            return newChunk;
        }

        public PathfindingNode GetNode(TileRef tile)
        {
            foreach (var chunk in GetChunks(tile.GridIndex))
            {
                if (chunk.TryGetNode(tile, out var node))
                {
                    return node;
                }
            }

            return null;
        }

        public override void Initialize()
        {
            IoCManager.InjectDependencies(this);
            _pathfinder.Initialize();

            // Handle all the base grid changes
            // Anything that affects traversal (i.e. collision layer) is handled separately.
            _mapManager.OnGridRemoved += (id => { _queuedUpdates.Enqueue(new GridRemoval(id)); });
            _mapManager.GridChanged += QueueGridChange;
            _mapManager.TileChanged += QueueTileChange;
        }

        public override void Shutdown()
        {
            base.Shutdown();
            _mapManager.OnGridRemoved -= (id => { _queuedUpdates.Enqueue(new GridRemoval(id)); });
            _mapManager.GridChanged -= QueueGridChange;
            _mapManager.TileChanged -= QueueTileChange;
        }

        private void QueueGridChange(object sender, GridChangedEventArgs eventArgs)
        {
            throw new NotImplementedException();
            _queuedUpdates.Enqueue(new GridChange()); // TODO
        }

        private void QueueTileChange(object sender, TileChangedEventArgs eventArgs)
        {
            _queuedUpdates.Enqueue(new TileUpdate(eventArgs.NewTile));
        }

        /// <summary>
        /// If an entity's collision gets turn on then we need to start tracking it as it moves to update the graph
        /// </summary>
        /// <param name="entity"></param>
        private void HandleCollidableAdd(IEntity entity)
        {
            // TODO: Should store tracked entities somewhere so if the component gets added after we can still function normally
            if (entity.HasComponent<ServerDoorComponent>() || entity.HasComponent<AirlockComponent>()) return;
            entity.Transform.OnMove += (sender, args) =>
            {
                QueueCollidableMove(sender, args, entity);
            };
            var grid = _mapManager.GetGrid(entity.Transform.GridID);
            var tileRef = grid.GetTileRef(entity.Transform.GridPosition);

            var collisionLayer = entity.GetComponent<CollidableComponent>().CollisionLayer;

            foreach (var chunk in GetChunks(entity.Transform.GridID))
            {
                if (chunk.TryGetNode(tileRef, out var node))
                {
                    node.AddCollisionLayer(collisionLayer);
                    break;
                }
            }

            _lastKnownPositions.Add(entity, tileRef);
        }

        /// <summary>
        /// If an entity's collision is removed then stop tracking it from the graph
        /// </summary>
        /// <param name="entity"></param>
        private void HandleCollidableRemove(IEntity entity)
        {
            if (entity.HasComponent<ServerDoorComponent>() || entity.HasComponent<AirlockComponent>()) return;
            entity.Transform.OnMove -= (sender, args) =>
            {
                QueueCollidableMove(sender, args, entity);
            };
            var grid = _mapManager.GetGrid(entity.Transform.GridID);
            var tileRef = grid.GetTileRef(entity.Transform.GridPosition);

            var collisionLayer = entity.GetComponent<CollidableComponent>().CollisionLayer;

            foreach (var chunk in GetChunks(entity.Transform.GridID))
            {
                if (chunk.TryGetNode(tileRef, out var node))
                {
                    node.RemoveCollisionLayer(collisionLayer);
                    break;
                }
            }

            _lastKnownPositions.Remove(entity);
        }

        // TODO: Longer term -> Handle collision layer changes?

        private void QueueCollisionEnabledEvent(object sender, CollisionEnabledEvent collisionEvent)
        {
            var entityManager = IoCManager.Resolve<IEntityManager>();
            var entity = entityManager.GetEntity(collisionEvent.Owner);
            switch (collisionEvent.Value)
            {
                case true:
                    _queuedUpdatesSync.Enqueue(new CollisionChange(entity, true));
                    break;
                case false:
                    _queuedUpdatesSync.Enqueue(new CollisionChange(entity, false));
                    break;
            }
        }

        public override void SubscribeEvents()
        {
            base.SubscribeEvents();
            SubscribeEvent<CollisionEnabledEvent>(QueueCollisionEnabledEvent);
        }
    }
}
