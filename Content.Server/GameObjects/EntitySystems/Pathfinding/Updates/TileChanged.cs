using Robust.Shared.Map;

namespace Content.Server.GameObjects.EntitySystems.Pathfinding.Updates
{
    public struct TileUpdate : IPathfindingGraphUpdate
    {
        public TileUpdate(TileRef tile)
        {
            Tile = tile;
        }

        public TileRef Tile { get; }
    }
}
