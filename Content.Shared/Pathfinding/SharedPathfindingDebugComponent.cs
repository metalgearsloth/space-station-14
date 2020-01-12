using System;
using System.Collections.Generic;
using Content.Shared.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Content.Shared.Pathfinding
{
    public abstract class SharedPathfindingDebugComponent : Component
    {
        public override string Name => "PathfindingDebugger";
        public override uint? NetID => ContentNetIDs.PATHFINDER_DEBUG;
        public PathfindingDebugMode Mode = PathfindingDebugMode.Route;
    }

    [Serializable, NetSerializable]
    public enum PathfindingDebugMode {
        None = 0,
        Route = 1 << 0,
        ConsideredTiles = 1 << 1,
        Graph = 1 << 2,
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

    [Serializable, NetSerializable]
    public class PathfindingRoute : ComponentMessage
    {
        public readonly List<Vector2> Route;
        public readonly Dictionary<Vector2, Vector2> CameFrom;
        public readonly Dictionary<Vector2, float> GScores;
        public readonly List<Vector2> ClosedTiles;
        public double TimeTaken;

        public PathfindingRoute(
            IEnumerable<TileRef> route,
            //IDictionary<TileRef, TileRef> cameFrom,
            IDictionary<TileRef, float> gScores,
            IEnumerable<TileRef> closedTiles,
            double timeTaken)
        {
            var mapManager = IoCManager.Resolve<IMapManager>();

            Route = new List<Vector2>();
            foreach (var tile in route)
            {
                var tileGrid = mapManager.GetGrid(tile.GridIndex).GridTileToLocal(tile.GridIndices);
                Route.Add(mapManager.GetGrid(tile.GridIndex).LocalToWorld(tileGrid).Position);
            }

            /*
            CameFrom = new Dictionary<Vector2, Vector2>();
            foreach (var (from, to) in cameFrom)
            {
                var tileOneGrid = mapManager.GetGrid(from.GridIndex).GridTileToLocal(from.GridIndices);
                var tileOneWorld = mapManager.GetGrid(from.GridIndex).LocalToWorld(tileOneGrid).Position;
                var tileTwoGrid = mapManager.GetGrid(to.GridIndex).GridTileToLocal(to.GridIndices);
                var tileTwoWorld = mapManager.GetGrid(to.GridIndex).LocalToWorld(tileTwoGrid).Position;
                CameFrom.Add(tileOneWorld, tileTwoWorld);
            }
            */

            GScores = new Dictionary<Vector2, float>();
            foreach (var (tile, score) in gScores)
            {
                var tileGrid = mapManager.GetGrid(tile.GridIndex).GridTileToLocal(tile.GridIndices);
                GScores.Add(mapManager.GetGrid(tile.GridIndex).LocalToWorld(tileGrid).Position, score);
            }

            ClosedTiles = new List<Vector2>();
            foreach (var tile in closedTiles)
            {
                var tileGrid = mapManager.GetGrid(tile.GridIndex).GridTileToLocal(tile.GridIndices);
                ClosedTiles.Add(mapManager.GetGrid(tile.GridIndex).LocalToWorld(tileGrid).Position);
            }

            TimeTaken = timeTaken;
        }
    }
}
