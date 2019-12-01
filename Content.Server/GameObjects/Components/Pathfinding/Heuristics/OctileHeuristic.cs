using System;
using Robust.Shared.Map;

namespace Content.Server.GameObjects.Components.Pathfinding.Heuristics
{
    internal class SimpleOctileHeuristic : IPathfindingHeuristic
    {
        public float GetTileCost(TileRef start, TileRef end)
        {
            // "Fast". Gets the rough euclidean distance
            // This implementation is written down in a few sources; it just saves doing sqrt.
            int dstX = Math.Abs(start.X - end.X);
            int dstY = Math.Abs(start.Y - end.Y);
            if (dstX > dstY)
            {
                return 1.4f * dstY + (dstX - dstY);
            }

            return 1.4f * dstX + (dstY - dstX);
        }
    }
}
