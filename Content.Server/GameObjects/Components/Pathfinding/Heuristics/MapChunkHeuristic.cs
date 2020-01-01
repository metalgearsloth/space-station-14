using System.Collections.Generic;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Map;

namespace Content.Server.GameObjects.Components.Pathfinding.Heuristics
{
    public class MapChunkHeuristic
    {
        private readonly Dictionary<GridId, Dictionary<MapIndices, IMapChunk>> _mapChunks =
            new Dictionary<GridId, Dictionary<MapIndices, IMapChunk>>();

        // Store roomsprivate readonly HashSet<IPathfindingRoom>

        void RefreshAllChunks()
        {
            var mapManager = IoCManager.Resolve<IMapManager>();

            foreach (var grid in mapManager.GetAllGrids())
            {
                foreach (var tile in grid.GetAllTiles())
                {
                    // Get chunk
                    //add to _mapChunks
                }
            }
        }

        void RefreshTile(TileRef tile)
        {
            return;
        }
    }
}
