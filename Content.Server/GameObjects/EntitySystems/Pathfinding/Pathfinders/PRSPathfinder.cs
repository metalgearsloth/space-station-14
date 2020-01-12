using System;
using System.Collections.Generic;
using Content.Shared.Pathfinding;
using Robust.Shared.Map;

namespace Content.Server.GameObjects.EntitySystems.Pathfinding.Pathfinders
{
    /// <summary>
    /// Parallel Ripple Search
    /// </summary>
    public class PrsPathfinder : IPathfinder
    {
        public event Action<PathfindingRoute> DebugRoute;
        public void Initialize()
        {
            throw new NotImplementedException();
        }

        public Queue<TileRef> FindPath(List<PathfindingChunk> graph, PathfindingArgs pathfindingArgs)
        {
            throw new NotImplementedException();
        }
    }
}
