using System.Collections.Generic;
using Content.Server.GameObjects.Components.Pathfinding;
using JetBrains.Annotations;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Server.GameObjects.EntitySystems
{
    /// <summary>
    /// This is the backend that pre-caches stuff for the PathfinderManager so it doesn't have to re-get everything for every path.
    /// </summary>
    [UsedImplicitly]
    public class PathfindingSystem : EntitySystem
    {
#pragma warning disable 649
        [Dependency] private readonly IMapManager _mapManager;
#pragma warning restore 649

        // Use an int to tracker how many entities are blocking this tile
        // TODO: Really only PathfindingComponent should be calling this during init
        internal static IDictionary<TileRef, int> BlockedTiles = new Dictionary<TileRef, int>();
        internal static IDictionary<TileRef, float> TileCosts = new Dictionary<TileRef, float>();
        private List<IEntity> _knownEntities = new List<IEntity>();

        public override void Initialize()
        {
            base.Initialize();
            EntityQuery = new TypeEntityQuery(typeof(PathfindingComponent));
        }

        // TODO: I'd prefer to do everything in the system but it seems easier to
        // handle the entity startup and shutdowns in the component itself, currently this spaghetti is separate.
        public override void Update(float frameTime)
        {
            // Essentially we check which entities relevant to pathfinding have changed their positions and if so update the pre-calculated stuff.
            // If we don't pre-calculate to some degreethere's a stutter every time we pathfind from grabbing all the entities.
            base.Update(frameTime);
            var tempKnownEntities = new List<IEntity>();
            foreach (var entity in RelevantEntities)
            {
                entity.TryGetComponent(out PathfindingComponent pathfindingComponent);

                // First check if exact position has changed, then check if the tile position has changed
                if (entity.Transform.GridPosition != pathfindingComponent.LastGrid)
                {
                    var entityTile = _mapManager
                        .GetGrid(entity.Transform.GridID)
                        .GetTileRef(entity.Transform.GridPosition);

                    var lastTile = _mapManager
                        .GetGrid(entity.Transform.GridID)
                        .GetTileRef(entity.Transform.GridPosition);

                    if (entityTile != lastTile)
                    {
                        // If it's already been seen update its old tile
                        if (_knownEntities.Contains(entity))
                        {
                            if (pathfindingComponent.Traversable)
                            {
                                BlockedTiles[lastTile] -= 1;
                            }
                            else
                            {
                                TileCosts[lastTile] -= pathfindingComponent.Cost;
                            }
                        }

                        if (pathfindingComponent.Traversable)
                        {
                            TileCosts.TryGetValue(entityTile, out var currentCost);
                            TileCosts[entityTile] = currentCost + pathfindingComponent.Cost;
                        }
                        else
                        {

                            BlockedTiles[lastTile] += 1;
                        }
                    }
                }

                tempKnownEntities.Add(entity);
            }

            // Cleanup
            var tempNewBlockers = new Dictionary<TileRef, int>();
            foreach (var (tile, refCount) in BlockedTiles)
            {
                if (refCount > 0)
                {
                    tempNewBlockers.Add(tile, refCount);
                }
            }

            _knownEntities = tempKnownEntities;
            BlockedTiles = tempNewBlockers;
        }

        /// <summary>
        /// Gets all the traversable tiles on a specified grid
        /// </summary>
        /// <param name="grid"></param>
        /// <returns></returns>
        internal static IEnumerable<TileRef> FreeTiles(GridId grid)
        {
            // Somewhat of a wrapper around GetAllTiles
            var mapManager = IoCManager.Resolve<IMapManager>();
            foreach (var tile in mapManager.GetGrid(grid).GetAllTiles())
            {
                if (BlockedTiles.ContainsKey(tile))
                {
                    continue;
                }
                yield return tile;
            }
        }

    }
}
