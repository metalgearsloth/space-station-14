using System.Collections;
using System.Collections.Generic;
using Content.Server.GameObjects.Components.Pathfinding;
using JetBrains.Annotations;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Map;

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
        internal static readonly IDictionary<TileRef, int> BlockedTiles = new Dictionary<TileRef, int>();
        internal static readonly IDictionary<TileRef, float> TileCosts = new Dictionary<TileRef, float>();

        /// <summary>
        /// Updates the relevant tile positions as required
        /// </summary>
        /// <param name="pathfindingComponent"></param>
        public void HandleEntityChange(PathfindingComponent pathfindingComponent)
        {
            var oldTile = _mapManager.GetGrid(pathfindingComponent.LastGrid.GridID).GetTileRef(pathfindingComponent.LastGrid);
            var newTile = _mapManager.GetGrid(pathfindingComponent.Owner.Transform.GridID).GetTileRef(pathfindingComponent.Owner.Transform.GridPosition);

            if (oldTile == newTile)
            {
                return;
            }

            if (!pathfindingComponent.Traversable)
            {
                if (BlockedTiles.ContainsKey(oldTile))
                {
                    BlockedTiles[oldTile]--;

                    if (BlockedTiles[oldTile] <= 0)
                    {
                        BlockedTiles.Remove(oldTile);
                    }
                }

                BlockedTiles.TryGetValue(newTile, out var newBlockValue);
                BlockedTiles[newTile] = newBlockValue + 1;
                return;
            }

            if (TileCosts.ContainsKey(oldTile))
            {
                TileCosts[oldTile] -= pathfindingComponent.Cost;
                if (TileCosts[oldTile] <= 0)
                {
                    TileCosts.Remove(oldTile);
                }
            }

            TileCosts.TryGetValue(newTile, out var newTileValue);
            TileCosts[newTile] = newTileValue + pathfindingComponent.Cost;
        }

        internal void HandleEntityAdd(PathfindingComponent pathfindingComponent)
        {
            var tile = _mapManager.GetGrid(pathfindingComponent.Owner.Transform.GridID)
                .GetTileRef(pathfindingComponent.Owner.Transform.GridPosition);

            switch (pathfindingComponent.Traversable)
            {
                case true:
                    TileCosts.TryGetValue(tile, out var costValue);
                    TileCosts[tile] = costValue;
                    return;
                case false:
                    BlockedTiles.TryGetValue(tile, out var blockValue);
                    BlockedTiles[tile] = blockValue;
                    return;
            }
        }

        internal void HandleEntityRemove(PathfindingComponent pathfindingComponent)
        {
            var tile = _mapManager.GetGrid(pathfindingComponent.Owner.Transform.GridID)
                .GetTileRef(pathfindingComponent.Owner.Transform.GridPosition);

            switch (pathfindingComponent.Traversable)
            {
                case true:
                    if (!TileCosts.ContainsKey(tile))
                    {
                        return;
                    }
                    TileCosts[tile] -= pathfindingComponent.Cost;
                    if (TileCosts[tile] <= 0)
                    {
                        TileCosts.Remove(tile);
                    }
                    return;
                case false:
                    if (!BlockedTiles.ContainsKey(tile))
                    {
                        return;
                    }
                    BlockedTiles[tile] -= 1;
                    if (BlockedTiles[tile] <= 0)
                    {
                        BlockedTiles.Remove(tile);
                    }
                    return;
            }
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
