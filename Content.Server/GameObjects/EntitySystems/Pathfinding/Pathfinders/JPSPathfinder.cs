using System;
using System.Collections.Generic;
using Content.Shared.Pathfinding;
using Robust.Shared.Map;

namespace Content.Server.GameObjects.EntitySystems.Pathfinding.Pathfinders
{
    public class JpsPathfinder : IPathfinder
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
