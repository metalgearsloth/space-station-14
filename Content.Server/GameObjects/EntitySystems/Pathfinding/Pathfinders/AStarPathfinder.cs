using System;
using System.Collections.Generic;
using Content.Server.GameObjects.Components.Pathfinding.PathfindingQueue;
using Content.Shared.Pathfinding;
using JetBrains.Annotations;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Server.GameObjects.EntitySystems.Pathfinding.Pathfinders
{
    public interface IPathfinder
    {
        event Action<PathfindingRoute> DebugRoute;

        void Initialize();

        /// <summary>
        ///  Find a tile path from start to end
        /// </summary>
        /// <param name="graph"></param>
        /// <param name="pathfindingArgs"></param>
        /// <returns></returns>
        Queue<TileRef> FindPath(List<PathfindingChunk> graph, PathfindingArgs pathfindingArgs);
    }

    public struct PathfindingArgs
    {
        public int CollisionMask { get; }
        public TileRef Start { get; }
        public TileRef End { get; }
        // How close we need to get to the endpoint to be 'done'
        public float Proximity { get; }
        // Whether we use cardinal only or not
        public bool AllowDiagonals { get; }
        // Can we go through walls
        public bool NoClip { get; }
        // Can we traverse space tiles
        public bool AllowSpace { get; }

        public PathfindingArgs(
            int collisionMask,
            TileRef start,
            TileRef end,
            float proximity = 0.0f,
            bool allowDiagonals = true,
            bool noClip = false,
            bool allowSpace = false)
        {
            CollisionMask = collisionMask;
            Start = start;
            End = end;
            Proximity = proximity;
            AllowDiagonals = allowDiagonals;
            NoClip = noClip;
            AllowSpace = allowSpace;
        }
    }

    [UsedImplicitly]
    public class AStarPathfinder : IPathfinder
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

        public event Action<PathfindingRoute> DebugRoute;
        private PathfindingSystem _pathfindingSystem;

        public void Initialize()
        {
            _pathfindingSystem = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<PathfindingSystem>();
        }

        // This isn't the greatest but it's definitely a lot faster than when I first wrote it, plus given it's run in a background thread any crappy performance shouldn't be noticeable for a while.
        public Queue<TileRef> FindPath(List<PathfindingChunk> graph, PathfindingArgs pathfindingArgs)
        {
            var startTile = pathfindingArgs.Start;
            var endTile = pathfindingArgs.End;

            if (startTile.GridIndex != endTile.GridIndex)
            {
                return null;
            }

            DateTime pathTimeStart = DateTime.Now;
            PathfindingNode startNode = _pathfindingSystem.GetNode(startTile);
            PathfindingNode endNode = _pathfindingSystem.GetNode(endTile);

            if (startNode == null)
            {
                // Logger.WarningS("pathfinding", $"No node found for {startTile}");
                return null;
            }

            if (endNode == null)
            {
                // Logger.WarningS("pathfinding", $"No node found for {endTile}");
                return null;
            }

            // Check if we can get a proximity tile to see if we can end early
            if (!Traversable(pathfindingArgs.CollisionMask, endNode.CollisionMask))
            {
                var newEndFound = false;
                if (pathfindingArgs.Proximity > 0.0f)
                {
                    // TODO: Should make this account for proximities, probably some kind of breadth-first search to find a valid one
                    foreach (var (direction, node) in endNode.Neighbors)
                    {
                        if (Traversable(pathfindingArgs.CollisionMask, node.CollisionMask))
                        {
                            endNode = node;
                            newEndFound = true;
                            break;
                        }
                    }
                }

                if (!newEndFound)
                {
                    return null;
                }
            }

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

                foreach (var (direction, next) in currentNode.Neighbors)
                {
                    if (closedTiles.Contains(next))
                    {
                        continue;
                    }

                    // TODO: If it's a diagonal we need to check NSEW to see if we can get to it and stop corner cutting, NE needs N and E etc.
                    // Given there's different collision layers stored for each node in the graph it's probably not worth it to cache this

                    currentNode.Neighbors.TryGetValue(Direction.North, out var northNeighbor);
                    currentNode.Neighbors.TryGetValue(Direction.South, out var southNeighbor);
                    currentNode.Neighbors.TryGetValue(Direction.East, out var eastNeighbor);
                    currentNode.Neighbors.TryGetValue(Direction.West, out var westNeighbor);

                    switch (direction)
                    {
                        case Direction.NorthEast:
                            if (northNeighbor == null || eastNeighbor == null) continue;
                            if (!Traversable(pathfindingArgs.CollisionMask, northNeighbor.CollisionMask) ||
                                !Traversable(pathfindingArgs.CollisionMask, eastNeighbor.CollisionMask))
                            {
                                continue;
                            }
                            break;
                        case Direction.NorthWest:
                            if (northNeighbor == null || westNeighbor == null) continue;
                            if (!Traversable(pathfindingArgs.CollisionMask, northNeighbor.CollisionMask) ||
                                !Traversable(pathfindingArgs.CollisionMask, westNeighbor.CollisionMask))
                            {
                                continue;
                            }
                            break;
                        case Direction.SouthWest:
                            if (southNeighbor == null || westNeighbor == null) continue;
                            if (!Traversable(pathfindingArgs.CollisionMask, southNeighbor.CollisionMask) ||
                                !Traversable(pathfindingArgs.CollisionMask, westNeighbor.CollisionMask))
                            {
                                continue;
                            }
                            break;
                        case Direction.SouthEast:
                            if (southNeighbor == null || eastNeighbor == null) continue;
                            if (!Traversable(pathfindingArgs.CollisionMask, southNeighbor.CollisionMask) ||
                                !Traversable(pathfindingArgs.CollisionMask, eastNeighbor.CollisionMask))
                            {
                                continue;
                            }
                            break;
                    }


                    // If tile is untraversable it'll be null
                    var tileCost = GetTileCost(pathfindingArgs.CollisionMask, pathfindingArgs, currentNode, next);

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
                // Logger.WarningS("pathfinding", $"No route from {startTile} to {endTile}");
                return null;
            }

            var route = ReconstructPath(cameFrom, currentNode);
            if (route.Count == 1)
            {
                return null;
            }
            var timeTaken = (DateTime.Now - pathTimeStart).TotalSeconds;

            // Need to get data into an easier format to send to the relevant clients
            if (DebugRoute != null)
            {
                /*
                var debugClosedTiles = new List<TileRef>();
                var debugGScores = new Dictionary<TileRef, float>();

                foreach (var (node, score) in gScores)
                {
                    debugGScores.Add(node.TileRef, score);
                }

                foreach (var node in closedTiles)
                {
                    debugClosedTiles.Add(node.TileRef);
                }

                var debugRoute = new PathfindingRoute(
                    route,
                    // cameFrom,
                    debugGScores,
                    debugClosedTiles,
                    timeTaken);

                DebugRoute.Invoke(debugRoute);
                */
            }

            return route;
        }

        public static Queue<TileRef> ReconstructPath(IDictionary<PathfindingNode, PathfindingNode> cameFrom, PathfindingNode current)
        {
            var running = new Stack<TileRef>();
            running.Push(current.TileRef);
            while (cameFrom.ContainsKey(current))
            {
                var previousCurrent = current;
                current = cameFrom[current];
                cameFrom.Remove(previousCurrent);
                running.Push(current.TileRef);
            }

            var result = new Queue<TileRef>(running);

            return result;
        }

        private bool Traversable(int collisionMask, int nodeMask)
        {
            return (collisionMask & nodeMask) == 0;
        }

        private float? GetTileCost(int collisionMask, PathfindingArgs pathfindingArgs, PathfindingNode start, PathfindingNode end)
        {

            if (!pathfindingArgs.NoClip && !Traversable(collisionMask, end.CollisionMask))
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
    }
}
