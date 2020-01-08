using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Content.Server.GameObjects.EntitySystems.Pathfinding;
using Robust.Server.GameObjects;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Map;

namespace Content.Server.GameObjects.EntitySystems
{
    // TODO: Need to fix multiple collision layers and also cache each tile's neighbors (or alternatively use room chunking)
    // TODO: Look at storing different graphs for each mask

    public class PathfindingSystem : EntitySystem
    {
        #pragma warning disable 649
        [Dependency] private readonly IMapManager _mapManager;
#pragma warning restore 649

        private readonly Dictionary<GridId, List<PathfindingChunk>> _graph = new Dictionary<GridId, List<PathfindingChunk>>();

        // Every frame we queue up all the changes and do them at once
        private readonly Stack<GridChangedEventArgs> _gridChanges = new Stack<GridChangedEventArgs>();
        private readonly List<TileChangedEventArgs> _tileChanges = new List<TileChangedEventArgs>();
        private readonly List<IEntity> _collidableAdds = new List<IEntity>();
        private readonly List<IEntity> _collidableRemoves = new List<IEntity>();
        private readonly Dictionary<IEntity, MoveEventArgs> _queuedMoveEvents = new Dictionary<IEntity, MoveEventArgs>();

        public List<PathfindingChunk> GetChunks(GridId gridId)
        {
            if (_graph.ContainsKey(gridId))
            {
                return _graph[gridId];
            }

            var newChunks = new List<PathfindingChunk>();

            var grid = _mapManager.GetGrid(gridId);

            foreach (var tile in grid.GetAllTiles())
            {
                newChunks.Add(GetChunk(tile));
            }

            _graph.Add(gridId, newChunks);
            return newChunks;
        }

        private PathfindingChunk CreateChunk(GridId gridId, MapIndices indices)
        {
            var newChunk = new PathfindingChunk(gridId, indices);
            // Need to add neighbor references
            if (_graph.TryGetValue(gridId, out var chunks))
            {
                foreach (var chunk in chunks)
                {
                    if (AreNeighbors(newChunk, chunk))
                    {
                        newChunk.AddNeighbor(chunk);
                        chunk.AddNeighbor(newChunk);
                    }
                }
            }
            else
            {
                _graph.Add(gridId, new List<PathfindingChunk>());
            }

            var grid = _mapManager.GetGrid(gridId);

            // TODO: Add nodes
            for (var x = 0; x < PathfindingChunk.ChunkSize; x++)
            {
                for (var y = 0; y < PathfindingChunk.ChunkSize; y++)
                {
                    var trueX = x + indices.X;
                    var trueY = y + indices.Y;
                    var tileRef = grid.GetTileRef(new MapIndices(trueX, trueY));
                    newChunk.AddTile(tileRef);
                }
            }

            newChunk.RefreshNodeNeighbors();
            _graph[gridId].Add(newChunk);
            return newChunk;
        }

        private bool AreNeighbors(PathfindingChunk chunk1, PathfindingChunk chunk2)
        {
            if (chunk1.GridId != chunk2.GridId) return false;
            if (Math.Abs(chunk1.Indices.X - chunk2.Indices.X) != PathfindingChunk.ChunkSize) return false;
            if (Math.Abs(chunk1.Indices.Y - chunk2.Indices.Y) != PathfindingChunk.ChunkSize) return false;
            return true;
        }

        // TODO: Each node should really cache their edges
        /// <summary>
        /// Get adjacent tiles to this one, duh
        /// </summary>
        /// <param name="currentNode"></param>
        /// <param name="allowDiagonals"></param>
        /// <returns></returns>
        public static IEnumerable<PathfindingNode> GetNeighbors(PathfindingNode currentNode, bool allowDiagonals = true)
        {
            foreach (var neighbor in currentNode.Neighbors)
            {
                yield return neighbor;
            }
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            /*
            HandleGridChanges(_gridChanges);
            _gridChanges.Clear();
            */

            HandleTileChanges(_tileChanges);
            _tileChanges.Clear();

            HandleCollisionAdds(_collidableAdds);
            _collidableAdds.Clear();

            HandleCollisionRemoves(_collidableRemoves);
            _collidableRemoves.Clear();

            HandleCollidableMoves(_queuedMoveEvents);
            _queuedMoveEvents.Clear();
        }

        private void RemovePathfindingGrid(GridId gridId)
        {
            if (!_graph.ContainsKey(gridId))
            {
                throw new InvalidOperationException();
            }

            _graph.Remove(gridId);
        }

        private PathfindingNode GetNodeTile(TileRef tile)
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

        private void UpdateNodeTile(TileRef tile)
        {
            foreach (var chunk in GetChunks(tile.GridIndex))
            {
                if (chunk.TryUpdateNode(tile))
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Runs through all the changed tiles this frame and updates the nodes
        /// </summary>
        /// <param name="tileChangedEvents"></param>
        private void HandleTileChanges(List<TileChangedEventArgs> tileChangedEvents)
        {
            if (tileChangedEvents.Count == 0) return;
            foreach (var tileEvent in tileChangedEvents)
            {
                UpdateNodeTile(tileEvent.NewTile);
            }
        }

        public override void Initialize()
        {
            // Handle all the base grid changes
            // Anything that affects traversal (i.e. collision layer) is handled separately.
            _mapManager.OnGridRemoved += RemovePathfindingGrid;
            _mapManager.GridChanged += (sender, args) => {_gridChanges.Push(args);};
            _mapManager.TileChanged += (sender, args) => { _tileChanges.Add(args); };
        }

        private PathfindingChunk GetChunk(TileRef tile)
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

            var chunkX = tile.X / PathfindingChunk.ChunkSize;
            var chunkY = tile.Y / PathfindingChunk.ChunkSize;

            var newChunk = CreateChunk(tile.GridIndex, new MapIndices(chunkX, chunkY));
            return newChunk;
        }

        /// <summary>
        /// Add the relevant collidable collision layers to nodes and also start watching the entity's movement
        /// </summary>
        /// <param name="collidableEntities"></param>
        private void HandleCollisionAdds(List<IEntity> collidableEntities)
        {
            if (collidableEntities.Count == 0) return;
            // TODO: Need to fix multiple collision layers coz it ain't workin
            var collidableUpdates = new Dictionary<GridId, List<Tuple<TileRef, int>>>();
            var updateGridIds = new HashSet<GridId>();

            foreach (var entity in collidableEntities)
            {
                entity.Transform.OnMove += (sender, args) =>
                {
                    _queuedMoveEvents.TryAdd(entity, args);
                };

                var grid = _mapManager.GetGrid(entity.Transform.GridID);

                var tileRef = grid.GetTileRef(entity.Transform.GridPosition);

                if (!collidableUpdates.ContainsKey(grid.Index))
                {
                    collidableUpdates.Add(grid.Index, new List<Tuple<TileRef, int>>());
                }

                var collisionLayer = entity.GetComponent<CollidableComponent>().CollisionLayer;

                collidableUpdates[grid.Index].Add(new Tuple<TileRef, int>(tileRef, collisionLayer));
                updateGridIds.Add(grid.Index);
            }

            // Add the relevant collision layers
            foreach (var gridId in updateGridIds)
            {
                var updates = collidableUpdates[gridId];
                foreach (var chunk in GetChunks(gridId))
                {
                    for (var i = updates.Count - 1; i > 0; i--)
                    {
                        var update = updates[i];
                        if (chunk.TryGetNode(update.Item1, out var node))
                        {
                            node.AddCollisionLayer(update.Item2);
                            updates.RemoveAt(i);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// If an entity is no longer collidable then it's not relevant for pathfinding so we'll update whatever node it's at to remove its collision layer
        /// </summary>
        /// <param name="collidableEntities"></param>
        private void HandleCollisionRemoves(List<IEntity> collidableEntities)
        {
            if (collidableEntities.Count == 0) return;
            var collidableUpdates = new Dictionary<GridId, List<Tuple<TileRef, int>>>();
            var updateGridIds = new HashSet<GridId>();

            foreach (var entity in collidableEntities)
            {
                entity.Transform.OnMove -= (sender, args) =>
                {
                    _queuedMoveEvents.TryAdd(entity, args);
                };

                var grid = _mapManager.GetGrid(entity.Transform.GridID);

                var tileRef = grid.GetTileRef(entity.Transform.GridPosition);

                if (!collidableUpdates.ContainsKey(grid.Index))
                {
                    collidableUpdates.Add(grid.Index, new List<Tuple<TileRef, int>>());
                }

                var collisionLayer = entity.GetComponent<CollidableComponent>().CollisionLayer;

                collidableUpdates[grid.Index].Add(new Tuple<TileRef, int>(tileRef, collisionLayer));
                updateGridIds.Add(grid.Index);
            }

            // Remove the relevant collision layers
            // TODO: Need to detect whether it was even tracked already?
            foreach (var gridId in updateGridIds)
            {
                var updates = collidableUpdates[gridId];
                foreach (var chunk in GetChunks(gridId))
                {
                    for (var i = updates.Count - 1; i > 0; i--)
                    {
                        var update = updates[i];
                        if (chunk.TryGetNode(update.Item1, out var node))
                        {
                            node.AddCollisionLayer(update.Item2);
                            updates.RemoveAt(i);
                        }
                    }
                }
            }
        }

        // TODO: Longer term -> Handle collision layer changes?

        private void HandleCollidableMoves(Dictionary<IEntity, MoveEventArgs> moveEvents)
        {
            if (moveEvents.Count == 0) return;
            var collidableRemoves = new Dictionary<GridId, List<Tuple<TileRef, int>>>();
            var collidableAdds = new Dictionary<GridId, List<Tuple<TileRef, int>>>();
            // Assume these entities have already started tracking
            foreach (var (entity, moveEvent) in moveEvents)
            {
                var oldTile = _mapManager.GetGrid(moveEvent.OldPosition.GridID).GetTileRef(moveEvent.OldPosition);
                var newTile = _mapManager.GetGrid(moveEvent.NewPosition.GridID).GetTileRef(moveEvent.NewPosition);
                // Roundinggggg
                if (oldTile == newTile)
                {
                    return;
                }

                var collisionLayer = entity.GetComponent<CollidableComponent>().CollisionLayer;

                var oldGrid = moveEvent.OldPosition.GridID;
                var newGrid = moveEvent.NewPosition.GridID;

                if (!collidableRemoves.ContainsKey(oldGrid))
                {
                    collidableRemoves.Add(oldGrid, new List<Tuple<TileRef, int>>());
                }

                if (!collidableAdds.ContainsKey(newGrid))
                {
                    collidableAdds.Add(newGrid, new List<Tuple<TileRef, int>>());
                }

                collidableRemoves[oldGrid].Add(new Tuple<TileRef, int>(oldTile, collisionLayer));
                collidableAdds[newGrid].Add(new Tuple<TileRef, int>(newTile, collisionLayer));
            }

            foreach (var (grid, removes) in collidableRemoves)
            {
                var chunks = _graph[grid];
                foreach (var chunk in chunks)
                {
                    if (removes.Count == 0) break;
                    for (var i = removes.Count - 1; i > 0; i--)
                    {
                        var remove = removes[i];
                        if (chunk.TryGetNode(remove.Item1, out var node))
                        {
                            node.RemoveCollisionLayer(remove.Item2);
                            removes.RemoveAt(i);
                        }
                    }
                }
            }

            foreach (var (grid, adds) in collidableAdds)
            {
                var chunks = _graph[grid];
                foreach (var chunk in chunks)
                {
                    if (collidableAdds.Count == 0) break;
                    for (var i = adds.Count - 1; i > 0; i--)
                    {
                        var add = adds[i];
                        if (chunk.TryGetNode(add.Item1, out var node))
                        {
                            node.AddCollisionLayer(add.Item2);
                            adds.RemoveAt(i);
                        }
                    }
                }
            }
        }

        private void QueueCollisionEnabledEvent(object sender, CollisionEnabledEvent collisionEvent)
        {
            var entityManager = IoCManager.Resolve<IEntityManager>();
            var entity = entityManager.GetEntity(collisionEvent.Owner);
            switch (collisionEvent.Value)
            {
                case true:
                    _collidableAdds.Add(entity);
                    break;
                case false:
                    _collidableRemoves.Add(entity);
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
