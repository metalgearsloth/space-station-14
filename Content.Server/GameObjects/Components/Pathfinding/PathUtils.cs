using System;
using System.Collections.Generic;
using Content.Server.GameObjects.EntitySystems;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Map;

namespace Content.Server.GameObjects.Components.Pathfinding
{
    public static class PathUtils
    {
        // These are expected to be re-used across most / all implementations of pathfinding
        public static bool IsTileTraversable(TileRef tile)
        {
            if (tile.Tile.IsEmpty)
            {
                return false;
            }

            // If we know there's a known blocker (walls, tablets, etc) here already and it's not dead
            return !PathfindingSystem.BlockedTiles.ContainsKey(tile);
        }

        /// <summary>
        /// Get adjacent tiles to this one, duh
        /// </summary>
        /// <param name="tileRef"></param>
        /// <param name="allowDiagonals"></param>
        /// <returns></returns>
        public static IEnumerable<TileRef> GetNeighbors(TileRef tileRef, bool allowDiagonals = true)
        {
            var mapManger = IoCManager.Resolve<IMapManager>();
            for (int x = -1; x < 2; x++)
            {
                for (int y = -1; y < 2; y++)
                {
                    if (x == 0 & y == 0)
                    {
                        continue;
                    }

                    if (!allowDiagonals && Math.Abs(x) == 1 && Math.Abs(y) == 1)
                    {
                        continue;
                    }

                    var neighborTile = mapManger
                        .GetGrid(tileRef.GridIndex)
                        .GetTileRef(new MapIndices(tileRef.GridIndices.X + x, tileRef.GridIndices.Y + y));

                    yield return neighborTile;
                }
            }
        }

        public static List<TileRef> ReconstructPath(IDictionary<TileRef, TileRef> cameFrom, TileRef current)
        {
            var result = new List<TileRef> {current};
            TileRef previousCurrent;
            while (cameFrom.ContainsKey(current))
            {
                previousCurrent = current;
                current = cameFrom[current];
                cameFrom.Remove(previousCurrent);
                result.Add(current);
            }

            result.Reverse();
            return result;
        }
    }
}
