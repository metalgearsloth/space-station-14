using System;
using System.Collections.Generic;
using Content.Shared.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Content.Shared.Pathfinding
{
    public abstract class SharedPathfindingDebugComponent : Component
    {
        public override string Name => "PathfindingDebugger";
        public override uint? NetID => ContentNetIDs.PATHFINDER_DEBUG;
        public int DebugMode { get; set; } = 1;
    }

    [Flags]
    [Serializable, NetSerializable]
    public enum PathfindingDebugMode {
        None = 0,
        Route = 1,
        ConsideredTiles = 2,
        Graph = 4,
    }

    [Serializable, NetSerializable]
    public class PathfindingGraphMessage : ComponentMessage
    {
        public Dictionary<int, List<Vector2>> Graph { get; }

        public PathfindingGraphMessage(Dictionary<int, List<Vector2>> graph)
        {
            Graph = graph;
        }
    }

    public class AStarRouteDebug
    {
        public Queue<TileRef> Route { get; }
        public Dictionary<TileRef, TileRef> CameFrom { get; }
        public Dictionary<TileRef, float> GScores { get; }
        public HashSet<TileRef> ClosedTiles { get; }
        public double TimeTaken { get; }

        public AStarRouteDebug(
            Queue<TileRef> route,
            Dictionary<TileRef, TileRef> cameFrom,
            Dictionary<TileRef, float> gScores,
            HashSet<TileRef> closedTiles,
            double timeTaken)
        {
            Route = route;
            CameFrom = cameFrom;
            GScores = gScores;
            ClosedTiles = closedTiles;
            TimeTaken = timeTaken;
        }
    }

    [Serializable, NetSerializable]
    public class AStarRouteMessage : ComponentMessage
    {
        public readonly IEnumerable<Vector2> Route;
        public readonly Dictionary<Vector2, Vector2> CameFrom;
        public readonly Dictionary<Vector2, float> GScores;
        public readonly List<Vector2> ClosedTiles;
        public double TimeTaken;

        public AStarRouteMessage(
            IEnumerable<Vector2> route,
            Dictionary<Vector2, Vector2> cameFrom,
            Dictionary<Vector2, float> gScores,
            IEnumerable<Vector2> closedTiles,
            double timeTaken)
        {
            Route = route;
            CameFrom = cameFrom;
            GScores = gScores;
            TimeTaken = timeTaken;
        }
    }
}
