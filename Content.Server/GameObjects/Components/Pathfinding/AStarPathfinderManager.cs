using System;
using System.Collections.Generic;
using Content.Server.GameObjects.Components.Pathfinding.PathfindingQueue;
using Content.Server.GameObjects.EntitySystems;
using Content.Shared.GameObjects.Components.Pathfinding;
using JetBrains.Annotations;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Server.GameObjects.Components.Pathfinding
{
    public interface IPathfinder
    {
        event Action<PathfindingRoute> DebugRoute;

        /// <summary>
        ///  Find a tile path from start to end
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="pathfindingArgs"></param>
        /// <returns></returns>
        List<TileRef> FindPath(TileRef start, TileRef end, PathfindingArgs pathfindingArgs = new PathfindingArgs());

        /// <summary>
        ///  Find a tile path from start to end.
        /// Is normally a wrapper around the other method.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="pathfindingArgs"></param>
        /// <returns></returns>
        List<TileRef> FindPath(GridCoordinates start, GridCoordinates end, PathfindingArgs pathfindingArgs = new PathfindingArgs());
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

        public List<TileRef> FindPath(GridCoordinates start, GridCoordinates end, PathfindingArgs pathfindingArgs = new PathfindingArgs())
        {
            var startTile = _mapManager.GetGrid(start.GridID).GetTileRef(start);
            var endTile = _mapManager.GetGrid(start.GridID).GetTileRef(end);
            return FindPath(startTile, endTile, pathfindingArgs);
        }

        public List<TileRef> FindPath(TileRef start, TileRef end, PathfindingArgs pathfindingArgs)
        {
            if (_mapManager.GetGrid(start.GridIndex) != _mapManager.GetGrid(end.GridIndex))
            {
                return null;
            }

            // TODO: Use the heuristic to get traversible and cut this early

            // Check if destination is legit
            var endIsValid = true;

            if (!PathUtils.IsTileTraversable(end))
            {
                endIsValid = false;
                if (pathfindingArgs.Proximity > 0)
                {
                    var grid = _mapManager.GetGrid(end.GridIndex);
                    var endPosition = _mapManager.GetGrid(end.GridIndex).GridTileToLocal(end.GridIndices).Position;

                    foreach (var tile in grid.GetTilesIntersecting(new Circle(endPosition, pathfindingArgs.Proximity)))
                    {
                        if (tile == end || !PathUtils.IsTileTraversable(tile))
                        {
                            continue;
                        }

                        Logger.DebugS("pathfinding", $"{end} is untraversable; found nearest neighbor {tile}");
                        endIsValid = true;
                        end = tile;
                        break;
                    }
                }
            }

            if (!endIsValid)
            {
                Logger.DebugS("pathfinding", $"End tile {end} is not traversable");
                return null;
            }

            // TODO: PauseManager for timeout
            DateTime pathTimeStart = DateTime.Now;

            // TODO: Look at Sebastian Lague https://www.youtube.com/watch?v=mZfyt03LDH4
            var openTiles = new PathfindingPriorityQueue<TileRef>();
            var gScores = new Dictionary<TileRef, float>();
            var cameFrom = new Dictionary<TileRef, TileRef>();
            var closedTiles = new HashSet<TileRef>();

            // See http://theory.stanford.edu/~amitp/GameProgramming/Heuristics.html#S7;
            // Helps to breaks ties
            const float pFactor = 1 + 1 / 1000;

            TileRef currentTile = start;
            openTiles.Enqueue(currentTile, 0);
            gScores[currentTile] = 0;
            bool routeFound = false;
            while (openTiles.Count > 0)
            {
                if (currentTile.Equals(end))
                {
                    routeFound = true;
                    break;
                }

                currentTile = openTiles.Dequeue();
                closedTiles.Add(currentTile);

                foreach (var next in PathUtils.GetNeighbors(currentTile, pathfindingArgs.AllowDiagonals))
                {
                    if (closedTiles.Contains(next))
                    {
                        continue;
                    }

                    // If tile is untraversable it'll be null
                    var tileCost = GetTileCost(pathfindingArgs, next, currentTile);

                    if (tileCost == null)
                    {
                        continue;
                    }

                    var gScore = gScores[currentTile] + tileCost.Value;

                    if (!gScores.ContainsKey(next) || gScore < gScores[next])
                    {
                        cameFrom[next] = currentTile;
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

            var route = PathUtils.ReconstructPath(cameFrom, currentTile);
            DebugRoute?.Invoke(new PathfindingRoute(
                route,
                cameFrom,
                gScores,
                closedTiles,
                (DateTime.Now - pathTimeStart).TotalSeconds));

            return route;
        }

        private static float? GetTileCost(PathfindingArgs pathfindingArgs, TileRef start, TileRef end)
        {
            var cost = 1.0f;

            switch (pathfindingArgs.AllowDiagonals)
            {
                case true:
                    // "Fast Euclidean" / octile.
                    // This implementation is written down in a few sources; it just saves doing sqrt.
                    int dstX = Math.Abs(start.X - end.X);
                    int dstY = Math.Abs(start.Y - end.Y);
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
                    cost *= (Math.Abs(start.X - end.X) + Math.Abs(start.Y - end.Y));
                    break;
            }

            if (!pathfindingArgs.NoClip && PathfindingSystem.BlockedTiles.ContainsKey(end))
            {
                return null;
            }

            if (!pathfindingArgs.AllowSpace && end.Tile.IsEmpty)
            {
                return null;
            }

            return cost;
        }
    }

    public enum PathHeuristic
    {
        Octile,
        Manhattan,
    }
}
