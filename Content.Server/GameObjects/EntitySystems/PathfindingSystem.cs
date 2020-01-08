using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
    // TODO: Need to make the graph way more efficient
    // TODO: Look at storing different graphs for each mask
    // TODO: Store the PathfindingNodes as a 2d array of [x, y]. also
    // TODO, do I even need to store the X and Ys considering they're the indices...
    // TODO: Use PathfindingChunks
    public struct PathfindingNode
    {
        // TODO: Add access ID here
        public TileRef TileRef { get; }
        public IEnumerable<int> CollisionLayers { get; }
        // TODO: Add a collision mask and every time a collision layer is added / removed update the mask

        public PathfindingNode(TileRef tileRef, IEnumerable<int> collisionLayers)
        {
            TileRef = tileRef;
            CollisionLayers = collisionLayers;
        }
    }

    public class PathfindingSystem : EntitySystem
    {
        #pragma warning disable 649
        [Dependency] private readonly IMapManager _mapManager;
#pragma warning restore 649

        private readonly List<List<PathfindingNode>> _graph = new List<List<PathfindingNode>>();
        private readonly IDictionary<GridId, List<PathfindingNode>> _gridNodes = new ConcurrentDictionary<GridId, List<PathfindingNode>>();

        private readonly Stack<GridChangedEventArgs> _gridChanges = new Stack<GridChangedEventArgs>();
        private readonly List<TileChangedEventArgs> _tileChanges = new List<TileChangedEventArgs>();
        private readonly List<IEntity> _collidableAdds = new List<IEntity>();
        private readonly List<IEntity> _collidableRemoves = new List<IEntity>();
        private readonly Dictionary<IEntity, MoveEventArgs> _queuedMoveEvents = new Dictionary<IEntity, MoveEventArgs>();

        // Need to store last known position of entities so that when they move the graph can be updated.
        private readonly IDictionary<IEntity, TileRef> _entityPositions = new Dictionary<IEntity, TileRef>();
        private bool _initialized;

        public List<PathfindingNode> GetNodes(GridId gridId)
        {
            // TODO: Is this retarded with concurrentdictionary, need to make a copy
            return _gridNodes[gridId];
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            if (!_initialized)
            {
                _initialized = true;
                foreach (var grid in _mapManager.GetAllGrids())
                {
                    TryInitializeGrid(grid.Index);
                }
            }

            foreach (var change in _gridChanges)
            {
                HandleGridChanged(change);
            }
            _gridChanges.Clear();

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
            if (!_gridNodes.ContainsKey(gridId))
            {
                throw new InvalidOperationException();
            }

            _gridNodes.Remove(gridId);
        }

        private bool TryInitializeGrid(GridId gridId)
        {
            if (_gridNodes.ContainsKey(gridId))
            {
                return false;
            }

            var newNodes = new List<PathfindingNode>();

            _gridNodes.Add(gridId, newNodes);
            var grid = _mapManager.GetGrid(gridId);
            foreach (var tile in grid.GetAllTiles(false))
            {
                var node = new PathfindingNode(tile, new int[]{});
                newNodes.Add(node);
            }

            _gridNodes[gridId] = newNodes;
            return true;
        }

        /// <summary>
        /// Runs through all the changed tiles this frame and updates the nodes
        /// </summary>
        /// <param name="tileChangedEvents"></param>
        private void HandleTileChanges(List<TileChangedEventArgs> tileChangedEvents)
        {
            // If any of the old tiles are space then that means there's no existing node for it already (TODO: Fix?)
            for (var i = tileChangedEvents.Count - 1; i > 0; i--) {
                var tile = tileChangedEvents[i];
                if (tile.OldTile.IsEmpty)
                {
                    _gridNodes[tile.NewTile.GridIndex].Add(new PathfindingNode(tile.NewTile, new int[]{}));
                    tileChangedEvents.RemoveAt(i);
                }
            }

            var grids = new Dictionary<GridId, List<TileChangedEventArgs>>();
            foreach (var tile in tileChangedEvents)
            {
                if (!grids.ContainsKey(tile.NewTile.GridIndex))
                {
                    grids.Add(tile.NewTile.GridIndex, new List<TileChangedEventArgs>());
                }

                grids[tile.NewTile.GridIndex].Add(tile);
            }

            // Update the existing tiles
            foreach (var (grid, tiles) in grids)
            {
                var nodes = _gridNodes[grid];
                for (var i = nodes.Count - 1; i > 0; i--)
                {
                    // If we can finish early because all nodes are found
                    if (tiles.Count == 0) break;

                    var node = nodes[i];

                    for (var j = tiles.Count - 1; j > 0; j--)
                    {
                        if (node.TileRef.GridIndices != tileChangedEvents[j].NewTile.GridIndices) continue;
                        nodes[i] = new PathfindingNode(tileChangedEvents[j].NewTile, nodes[i].CollisionLayers);
                        tileChangedEvents.RemoveAt(j);
                        break;
                    }
                }
            }
        }

        // TODO: Test me
        private void HandleGridChanged(GridChangedEventArgs eventArgs)
        {
            if (TryInitializeGrid(eventArgs.Grid.Index))
            {
                return;
            }

            var grid = _mapManager.GetGrid(eventArgs.Grid.Index);
            var nodes = _gridNodes[eventArgs.Grid.Index];
            var modifiedNodes = new Stack<MapIndices>();

            for (var i = nodes.Count - 1; i > 0; i--)
            {
                foreach (var (position, _) in eventArgs.Modified)
                {
                    if (!modifiedNodes.Contains(position) && nodes[i].TileRef.GridIndices == position)
                    {
                       var tileRef = grid.GetTileRef(position);
                       nodes[i] = new PathfindingNode(tileRef, new int[]{});
                       modifiedNodes.Push(position);
                       break;
                    }
                }
            }

            foreach (var modified in eventArgs.Modified)
            {
                if (!modifiedNodes.Contains(modified.position))
                {
                    var tileRef = grid.GetTileRef(modified.position);
                    nodes.Add(new PathfindingNode(tileRef, new int[]{}));
                }
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

        /// <summary>
        /// Add the relevant collidable collision layers to nodes and also start watching the entity's movement
        /// </summary>
        /// <param name="collidableEntities"></param>
        private void HandleCollisionAdds(List<IEntity> collidableEntities)
        {
            // TODO: Need to fix multiple collision layers coz it ain't workin
            var collidableUpdates = new Dictionary<GridId, List<Tuple<TileRef, int>>>();

            foreach (var entity in collidableEntities)
            {
                entity.Transform.OnMove += (sender, args) =>
                {
                    _queuedMoveEvents.TryAdd(entity, args);
                };

                var grid = _mapManager.GetGrid(entity.Transform.GridID);

                var tileRef = grid.GetTileRef(entity.Transform.GridPosition);
                // Add the entity to their watched positions. The movement handler will use this to check if the entity's node needs updating
                _entityPositions.Add(entity, tileRef);

                if (!collidableUpdates.ContainsKey(grid.Index))
                {
                    collidableUpdates.Add(grid.Index, new List<Tuple<TileRef, int>>());
                }

                var collisionLayer = entity.GetComponent<CollidableComponent>().CollisionLayer;

                collidableUpdates[grid.Index].Add(new Tuple<TileRef, int>(tileRef, collisionLayer));

                if (entity.Name.Contains("APC"))
                {

                }
            }

            // Update the collision layers for the appropriate nodes
            foreach (var (grid, collisions) in collidableUpdates)
            {
                var nodes = _gridNodes[grid];
                for (var i = 0; i < nodes.Count; i++)
                {
                    if (collisions.Count == 0) break;
                    var node = nodes[i];
                    for (var j = collisions.Count - 1; j > 0; j--)
                    {
                        var collision = collisions[j];

                        if (node.TileRef != collision.Item1) continue;
                        var newLayer = node.CollisionLayers.ToList();
                        if (newLayer.Count > 0)
                        {

                        }
                        newLayer.Add(collision.Item2);
                        nodes[i] = new PathfindingNode(collision.Item1, newLayer.ToArray());
                        collisions.RemoveAt(j);
                        break;
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
            var collidableUpdates = new Dictionary<GridId, List<CollidableComponent>>();
            // If the entity isn't already tracked then no point doing anything
            foreach (var entity in collidableEntities)
            {
                entity.Transform.OnMove -= (sender, args) => {_queuedMoveEvents.TryAdd(entity, args);}; // TODO: Check this
                if (_entityPositions.ContainsKey(entity))
                {
                    _entityPositions.Remove(entity);
                    var gridId = entity.Transform.GridID;
                    if (!collidableUpdates.ContainsKey(gridId))
                    {
                        collidableUpdates.Add(gridId, new List<CollidableComponent>());
                    }
                    collidableUpdates[gridId].Add(entity.GetComponent<CollidableComponent>());
                }
            }

            // If we added its collision layer we now need to remove it
            foreach (var (grid, collidables) in collidableUpdates)
            {
                var nodes = _gridNodes[grid];
                for (var i = 0; i < nodes.Count; i++)
                {
                    // Unless the node's in a corner then we should be able to break early
                    if (collidables.Count == 0) break;
                    var node = nodes[i];
                    for (var j = collidables.Count - 1; j > 0; j--)
                    {
                        // TODO: Should use the _entityPositions cached TileRef
                        var tileRef = _mapManager.GetGrid(grid).GetTileRef(collidables[j].Owner.Transform.GridPosition);
                        if (node.TileRef == tileRef)
                        {
                            var newLayer = node.CollisionLayers.ToList();
                            newLayer.Remove(collidables[j].CollisionLayer);
                            nodes[i] = new PathfindingNode(nodes[i].TileRef, newLayer.ToArray());
                            collidables.RemoveAt(j);
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
                // TODO: Up to here, need to then iterate over all nodes
            }

            foreach (var (grid, removes) in collidableRemoves)
            {
                collidableAdds.TryGetValue(grid, out var adds);
                if (adds == null) adds = new List<Tuple<TileRef, int>>();
                var nodes = _gridNodes[grid];
                for (var i = 0; i < nodes.Count; i++)
                {
                    if (removes.Count == 0 && adds.Count == 0) break;
                    var node = nodes[i];
                    // It's possible for multiple collidables to be on the same tile so we can't break upon first occurence
                    for (var j = removes.Count - 1; j > 0; j--)
                    {
                        if (node.TileRef == removes[j].Item1)
                        {
                            var newLayer = node.CollisionLayers.ToList();
                            newLayer.Remove(removes[j].Item2);
                            nodes[i] = new PathfindingNode(node.TileRef, newLayer.ToArray());
                            removes.RemoveAt(j);
                        }
                    }

                    for (var j = adds.Count - 1; j > 0; j--)
                    {
                        if (node.TileRef == adds[j].Item1)
                        {
                            var newLayer = node.CollisionLayers.ToList();
                            newLayer.Remove(adds[j].Item2);
                            nodes[i] = new PathfindingNode(node.TileRef, newLayer.ToArray());
                            adds.RemoveAt(j);
                        }
                    }
                }
            }

            // If we got lucky then all the grids have been taken care of already
            foreach (var (grid, adds) in collidableAdds)
            {
                var nodes = _gridNodes[grid];
                for (var i = 0; i < nodes.Count; i++)
                {
                    if (adds.Count == 0) break;
                    var node = nodes[i];
                    for (var j = adds.Count - 1; j > 0; j--)
                    {
                        if (node.TileRef == adds[j].Item1)
                        {
                            // TODO: make the node updater a function probably
                            var newLayer = node.CollisionLayers.ToList();
                            newLayer.Remove(adds[j].Item2);
                            nodes[i] = new PathfindingNode(node.TileRef, newLayer.ToArray());
                            adds.RemoveAt(j);
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
