using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.GameObjects.Components.Pathfinding.PathfindingQueue;
using Content.Server.GameObjects.EntitySystems;
using Content.Server.GameObjects.EntitySystems.Pathfinding;
using Content.Shared.GameObjects.Components.Pathfinding;
using JetBrains.Annotations;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Map;

namespace Content.Server.GameObjects.Components.Pathfinding
{
    public interface IPathfinder
    {
        event Action<PathfindingRoute> DebugRoute;

        /// <summary>
        ///  Find a tile path from start to end
        /// </summary>
        /// <param name="collisionMask"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="pathfindingArgs"></param>
        /// <returns></returns>
        IReadOnlyCollection<TileRef> FindPath(int collisionMask, TileRef start, TileRef end, PathfindingArgs pathfindingArgs = new PathfindingArgs());

        /// <summary>
        ///  Find a tile path from start to end.
        /// Is normally a wrapper around the other method.
        /// </summary>
        /// <param name="collisionMask"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="pathfindingArgs"></param>
        /// <returns></returns>
        IReadOnlyCollection<TileRef> FindPath(int collisionMask, GridCoordinates start, GridCoordinates end, PathfindingArgs pathfindingArgs = new PathfindingArgs());
    }

    public struct PathfindingArgs
    {
        // How close we need to get to the endpoint to be 'done'
        public float Proximity { get; }
        // Whether we use cardinal only or not
        public bool AllowDiagonals { get; }
        // Can we go through walls
        public bool NoClip { get; }
        // Can we traverse space tiles
        public bool AllowSpace { get; }

        public PathfindingArgs(
            float proximity = 0.0f,
            bool allowDiagonals = true,
            bool noClip = false,
            bool allowSpace = false)
        {
            Proximity = proximity;
            AllowDiagonals = allowDiagonals;
            NoClip = noClip;
            AllowSpace = allowSpace;
        }
    }

    [UsedImplicitly]
    public class PathfindingManager : IPathfinder
    {

        // IMPORTANT NOTE:
        // There's only so far the choice of algorithm can take you. (JPS, D*, Theta*, etc)
        // At a certain point you need to start trimming the graph with heuristics / "Hierarchical pathfinding"
        // such as

        // HPA* (seems to be what factorio / rimworld use) -> Could potentially use MapChunk but I don't imagine you get many gains until the graph is massive
        // Swamps
        // Gateways
        // Dead-ends

        // Other searches to try besides A* in order of likely best:
        // JPS
        // Parallel Ripple Search
        // Theta*
        // D*
        // Bi-Directional A*
        // HPS(?)

// Ideally you'd store each room on the station and which room(s) it connects to.
// Then you'd be able to get a high-level overview of which rooms you need to go to,
// and you could probably run it asynchronously if the perf is better (connecting path from airlock to airlock).

// Reading material:
// https://harablog.wordpress.com/2011/09/07/jump-point-search/
// http://theory.stanford.edu/~amitp/GameProgramming/Variations.html
// http://theory.stanford.edu/~amitp/GameProgramming/Heuristics.html

// TODO: Look at using a new heap
// https://github.com/BlueRaja/High-Speed-Priority-Queue-for-C-Sharp


// So currently this is split into a system and a manager
// The system essentially tries to pre-cache shit where possible.
// Everything to do with working out the actual path should be in the manager.


#pragma warning disable 649
        [Dependency] private readonly IMapManager _mapManager;
#pragma warning restore 649

        // TODO: Add cached paths and interface it with rooms?

        public event Action<PathfindingRoute> DebugRoute;

        public IReadOnlyCollection<TileRef> FindPath(int collisionMask, GridCoordinates start, GridCoordinates end, PathfindingArgs pathfindingArgs = new PathfindingArgs())
        {
            var startTile = _mapManager.GetGrid(start.GridID).GetTileRef(start);
            var endTile = _mapManager.GetGrid(start.GridID).GetTileRef(end);
            return FindPath(collisionMask, startTile, endTile, pathfindingArgs);
        }

        // TODO: Test this asynchronously
        public IReadOnlyCollection<TileRef> FindPath(int collisionMask, TileRef start, TileRef end, PathfindingArgs pathfindingArgs)
        {
            if (_mapManager.GetGrid(start.GridIndex) != _mapManager.GetGrid(end.GridIndex))
            {
                return null;
            }

            DateTime pathTimeStart = DateTime.Now;
            var entitySystems = IoCManager.Resolve<IEntitySystemManager>();
            var pathChunks = entitySystems.GetEntitySystem<PathfindingSystem>().GetChunks(start.GridIndex);
            PathfindingNode startNode = null;
            PathfindingNode endNode = null;

            foreach (var chunk in pathChunks)
            {
                if (startNode != null && endNode != null) break;

                if (chunk.InBounds(start))
                {
                    chunk.TryGetNode(start, out var node);
                    startNode = node;
                }
                if (chunk.InBounds(end))
                {
                    chunk.TryGetNode(start, out var node);
                    endNode = node;
                }
            }


            if (startNode == null)
            {
                Logger.WarningS("pathfinding", $"No node found for {start}");
                return null;
            }

            if (endNode == null)
            {
                Logger.WarningS("pathfinding", $"No node found for {end}");
                return null;
            }

            // TODO: PauseManager for timeout

            // TODO: Look at Sebastian Lague https://www.youtube.com/watch?v=mZfyt03LDH4
            var openTiles = new PathfindingPriorityQueue<PathfindingNode>();
            var gScores = new Dictionary<PathfindingNode, float>();
            var cameFrom = new Dictionary<PathfindingNode, PathfindingNode>();
            var closedTiles = new HashSet<PathfindingNode>();

            // See http://theory.stanford.edu/~amitp/GameProgramming/Heuristics.html#S7;
            // Helps to breaks ties
            const float pFactor = 1 + 1 / 1000;

            // TODO: Look at trimming the graph during iteration given it's a copy instead of using closedTiles
            PathfindingNode? currentNode = startNode;
            openTiles.Enqueue(currentNode, 0);
            gScores[currentNode] = 0.0f;
            bool routeFound = false;
            while (openTiles.Count > 0)
            {
                if (currentNode.Equals(endNode))
                {
                    routeFound = true;
                    break;
                }

                currentNode = openTiles.Dequeue();
                closedTiles.Add(currentNode);

                foreach (var next in PathfindingSystem.GetNeighbors(currentNode, pathfindingArgs.AllowDiagonals))
                {
                    if (closedTiles.Contains(next))
                    {
                        continue;
                    }

                    // If tile is untraversable it'll be null
                    var tileCost = GetTileCost(collisionMask, pathfindingArgs, next, currentNode);

                    if (tileCost == null)
                    {
                        continue;
                    }

                    var gScore = gScores[currentNode] + tileCost.Value;

                    if (!gScores.ContainsKey(next) || gScore < gScores[next])
                    {
                        cameFrom[next] = currentNode;
                        gScores[next] = gScore;
                        // pFactor is tie-breaker. Not implemented in the heuristic itself
                        float fScore = gScores[next] + tileCost.Value * pFactor;
                        openTiles.Enqueue(next, fScore);
                    }
                }
            }

            if (!routeFound)
            {
                Logger.WarningS("pathfinding", $"No route from {start} to {end}");
                return null;
            }

            var timeTaken = (DateTime.Now - pathTimeStart).TotalSeconds;

            var route = ReconstructPath(cameFrom, currentNode);

            if (DebugRoute != null)
            {
                var debugClosedTiles = new Stack<TileRef>(closedTiles.Count);
                foreach (var tile in closedTiles)
                {
                    debugClosedTiles.Push(tile.TileRef);
                }

                var debugGScores = new Dictionary<TileRef, float>();
                foreach (var (node, value) in gScores)
                {
                    debugGScores.Add(node.TileRef, value);
                }

                var debugRoute = new PathfindingRoute(
                    route,
                    // cameFrom,
                    debugGScores,
                    debugClosedTiles,
                    timeTaken);

                DebugRoute.Invoke(debugRoute);
            }

            Logger.DebugS("pathfinding", $"Found path in {timeTaken} seconds");

            return route;
        }

        private bool Traversable(int collisionMask, IEnumerable<int> collisionlayers)
        {
            foreach (var layer in collisionlayers)
            {
                if ((collisionMask & layer) != 0) return false;
            }

            return true;
        }

        private float? GetTileCost(int collisionMask, PathfindingArgs pathfindingArgs, PathfindingNode start, PathfindingNode end)
        {

            if (!pathfindingArgs.NoClip && !Traversable(collisionMask, end.CollisionLayers))
            {
                return null;
            }

            if (!pathfindingArgs.AllowSpace && end.TileRef.Tile.IsEmpty)
            {
                return null;
            }

            var cost = 1.0f;

            switch (pathfindingArgs.AllowDiagonals)
            {
                case true:
                    // "Fast Euclidean" / octile.
                    // This implementation is written down in a few sources; it just saves doing sqrt.
                    int dstX = Math.Abs(start.TileRef.X - end.TileRef.X);
                    int dstY = Math.Abs(start.TileRef.Y - end.TileRef.Y);
                    if (dstX > dstY)
                    {
                        cost *= 1.4f * dstY + (dstX - dstY);
                    }
                    else
                    {
                        cost *= 1.4f * dstX + (dstY - dstX);
                    }
                    break;
                // Manhattan distance
                case false:
                    cost *= (Math.Abs(start.TileRef.X - end.TileRef.X) + Math.Abs(start.TileRef.Y - end.TileRef.Y));
                    break;
            }

            return cost;
        }

        public static List<TileRef> ReconstructPath(IDictionary<PathfindingNode, PathfindingNode> cameFrom, PathfindingNode current)
        {
            var result = new List<TileRef>();
            while (cameFrom.ContainsKey(current))
            {
                var previousCurrent = current;
                current = cameFrom[current];
                cameFrom.Remove(previousCurrent);
                result.Add(current.TileRef);
            }

            result.Reverse();
            return result;
        }
    }
}
