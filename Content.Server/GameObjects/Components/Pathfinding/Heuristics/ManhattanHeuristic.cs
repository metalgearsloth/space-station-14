using System;
using Robust.Shared.Map;

namespace Content.Server.GameObjects.Components.Pathfinding.Heuristics
{
    /// <summary>
    ///  Ideal for non-diagonal movement
    /// </summary>
    public class ManhattanHeuristic : IPathfindingHeuristic
    {
        public float GetTileCost(TileRef start, TileRef end)
        {
            return 1 * (Math.Abs(start.X - end.X) + Math.Abs(start.Y - end.Y));
        }
    }
}
