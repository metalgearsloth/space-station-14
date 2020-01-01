using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.GameObjects.EntitySystems;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Map;

namespace Content.Server.GameObjects.Components.Pathfinding.Heuristics
{
    public class GatewayHeuristic : IPathfindingHeuristic
    {
        // https://pdfs.semanticscholar.org/4707/464ecdbee31d2752fa66574e70e8e959ea1b.pdf
        public IEnumerable<IPathfindingRegion> Regions => _regions;
        private List<IPathfindingRegion> _regions = new List<IPathfindingRegion>();

        public GatewayHeuristic()
        {
            Initialize();
        }

        private List<IPathfindingRegion> GetRegionPath(TileRef start, TileRef end)
        {
            throw new NotImplementedException();
        }

        public float GetTileCost(TileRef start, TileRef end)
        {
            var validRegions = GetRegionPath(start, end);
            // Tile is technically traversable but we really don't want to prioritise it
            foreach (var region in validRegions)
            {
                foreach (var regionTile in region.GetTiles())
                {
                    if (regionTile == start)
                    {
                        return 1.0f;
                    }
                }
            }

            return 100.0f;
        }

        private void Initialize()
        {
            // For each grid run RefreshRegions
        }

        private void RefreshRegions(GridId gridId)
        {
            var mapManager = IoCManager.Resolve<IMapManager>();
            _regions = new List<IPathfindingRegion>();
            var queue = new Queue<TileRef>();
            foreach (var tile in PathfindingSystem.FreeTiles(gridId))
            {
                queue.Enqueue(tile);
            }

            var currentRegion = new GatewayRegion();
            _regions.Add(currentRegion);

            while (true)
            {
                // Start at the top-left free tile
                var currentTile = queue.Dequeue();
                bool shrunkR = false;
                bool shrunkL = false;
                currentRegion.AddTile(currentTile);

                var topRightNeighbor = mapManager
                    .GetGrid(gridId)
                    .GetTileRef(new MapIndices(currentTile.X + 1, currentTile.Y - 1));

                while (PathUtils.IsTileTraversable(currentTile) && PathUtils.IsTileTraversable(topRightNeighbor))
                {
                    currentTile = queue.Dequeue();
                    currentRegion.AddTile(currentTile);
                }

                var topNeighbor = mapManager
                    .GetGrid(gridId)
                    .GetTileRef(new MapIndices(currentTile.X, currentTile.Y - 1));

                if (currentRegion.Contains(currentTile))
                {
                    shrunkR = true;
                }
                // Erase the previous lines
                else if (!currentRegion.Contains(topNeighbor) && shrunkR)
                {
                    while (currentRegion.Contains(currentTile))
                    {
                        queue.Enqueue(currentTile);
                        currentTile = mapManager
                            .GetGrid(gridId)
                            .GetTileRef(new MapIndices(currentTile.X - 1, currentTile.Y));
                    }
                }
                // Get next row leftmost X
                foreach (var tile in queue)
                {
                    if (currentTile.Y > tile.Y)
                    {

                    }
                }
            }
        }

        private IPathfindingRegion GetTileRegion(TileRef tile)
        {
            foreach (var region in Regions)
            {
                foreach (var regionTile in region.GetTiles())
                {
                    if (tile == regionTile)
                    {
                        return region;
                    }
                }
            }

            Logger.FatalS("pathfinding", $"Couldn't find region for {tile}");
            throw new InvalidOperationException();
        }

        public void RefreshTile(TileRef tile)
        {
            // Starts the linefile again for that region
        }
    }
}
