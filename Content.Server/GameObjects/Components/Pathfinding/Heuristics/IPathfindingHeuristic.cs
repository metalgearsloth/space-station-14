using Robust.Shared.Map;

namespace Content.Server.GameObjects.Components.Pathfinding.Heuristics
{
    public interface IPathfindingHeuristic
    {
        float GetTileCost(TileRef start, TileRef end);
    }
}
