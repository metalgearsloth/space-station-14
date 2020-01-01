using System.Collections.Generic;
using Robust.Shared.Map;

namespace Content.Server.GameObjects.Components.Pathfinding.Heuristics
{
    public class GatewayRegion : IPathfindingRegion
    {
        public List<IPathfindingRegion> Neighbors { get; set; }

        private List<TileRef> _tiles = new List<TileRef>();

        public void AddTile(TileRef tile)
        {
            if (!PathUtils.IsTileTraversable(tile))
            {
                return;
            }

            _tiles.Add(tile);
        }

        public IEnumerable<TileRef> GetTiles()
        {
            foreach (var tile in _tiles)
            {
                yield return tile;
            }
        }

        public bool Contains(TileRef tile)
        {
            if (_tiles.Contains(tile))
            {
                return true;
            }

            return false;
        }
    }
}
