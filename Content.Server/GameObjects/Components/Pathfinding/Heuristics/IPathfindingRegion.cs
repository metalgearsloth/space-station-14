using System.Collections.Generic;
using Robust.Shared.Map;

namespace Content.Server.GameObjects.Components.Pathfinding.Heuristics
{
    public interface IPathfindingRegion
    {
        IEnumerable<TileRef> GetTiles();
        List<IPathfindingRegion> Neighbors { get; set; }
        bool Contains(TileRef tile);
    }
}
