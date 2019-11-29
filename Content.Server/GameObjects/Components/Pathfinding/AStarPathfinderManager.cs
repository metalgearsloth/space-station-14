using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Content.Server.Pathfinding;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Server.Interfaces.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Map;
using YamlDotNet.Core.Tokens;

namespace Content.Server.GameObjects.Components.Pathfinding
{
    public interface IPathfinder
    {
        IEnumerable<TileRef> FindPath(TileRef start, TileRef end);
        IEnumerable<TileRef> FindPath(GridCoordinates start, GridCoordinates end);
        double GetTileCost(TileRef tile);
    }

    public struct PathfindingRoute
    {
        private List<TileRef> Route { get; }
        public PathfindingRoute(List<TileRef> route)
        {
            Route = route;
        }
    }

    struct PathBlocker
    {
        public IEntity Entity { get; }
        public GridCoordinates Position { get; }
        public PathBlocker(IEntity entity)
        {
            Entity = entity;
            Position = entity.Transform.GridPosition;
        }
    }

    [UsedImplicitly]
    public class AStarPathfinder : IPathfinder
    {
        // TODO: Handle client requests and return them in a separate pathfinding component

// Ideally you'd store each room on the station and which room(s) it connects to.
// Then you'd be able to get a high-level overview of which rooms you need to go to,
// and you could probably run it asynchronously if the perf is better (connecting path from airlock to airlock).
#pragma warning disable 649
        [Dependency] private readonly IMapManager _mapManager;
        [Dependency] private readonly IServerEntityManager _serverEntityManager;
#pragma warning restore 649

        private float _timeout = 1.0f;
        private readonly IPathfindingPriorityQueue<TileRef> _openTiles = new PathfindingPriorityQueue<TileRef>();
        private readonly IDictionary<TileRef, double> _closedTiles = new ConcurrentDictionary<TileRef, double>();

        // TODO: REMOVE ON PR
        private List<IEntity> _spawnedTiles = new List<IEntity>();
        public event Action<PathfindingRoute> FoundRoute;

        // Anything you can do to speedup performance goes here
        // - Anything that physically blocks movement
        private IDictionary<TileRef, PathBlocker> _knownBlockers = new ConcurrentDictionary<TileRef, PathBlocker>();

        public IEnumerable<TileRef> FindPath(GridCoordinates start, GridCoordinates end)
        {
            var startTile = _mapManager.GetGrid(start.GridID).GetTileRef(start);
            var endTile = _mapManager.GetGrid(start.GridID).GetTileRef(end);
            return FindPath(startTile, endTile);
        }

        public IEnumerable<TileRef> FindPath(TileRef start, TileRef end)
        {
            var route = new List<TileRef>();

            // Check if the tilecost of the destination is valid to end early
            if (GetTileCost(end) == 0)
            {
                return route;
            }

            DateTime pathTimeStart = DateTime.Now;
            // scabbed from https://www.redblobgames.com/pathfinding/a-star/implementation.html#csharp
            // TODO: Look at Sebastian Lague https://www.youtube.com/watch?v=mZfyt03LDH4
            _openTiles.Clear();
            _closedTiles.Clear();
            _openTiles.Enqueue(start, 0);

            IDictionary<TileRef, TileRef> cameFrom = new ConcurrentDictionary<TileRef, TileRef>();
            TileRef currentTile = start;
            _closedTiles[currentTile] = 0;
            var neighborTasks = new List<Task>(8);

            while (_openTiles.Count > 0)
            {
                // TODO: Disabled just for debugging
                //if ((DateTime.Now - pathTimeStart).TotalSeconds > _timeout)
                //{
                //    _openTiles.Clear();
                //    _closedTiles.Clear();
                //    return route;
                //}
                currentTile = _openTiles.Dequeue();

                if (currentTile.Equals(end))
                {
                    break;
                }

                var neighbors = GetNeighbors(currentTile);

                foreach (var next in neighbors)
                {
                    var tile = currentTile;
                    neighborTasks.Add(Task.Run(() =>
                    {
                        var tileCost = GetTileCost(next);
                        if (tileCost == 0)
                        {
                            return;
                        }
                        double newCost = _closedTiles[tile] + tileCost;
                        if (!_closedTiles.ContainsKey(next) || newCost < _closedTiles[next])
                        {
                            _closedTiles[next] = newCost;
                            //double priority = newCost + ManhattanDistance(next, end);
                            double priority = newCost + EuclideanDistance(next, end);
                            _openTiles.Enqueue(next, priority);
                            cameFrom[next] = tile;
                        }
                    }));
                }

                Task.WaitAll(neighborTasks.ToArray());
                neighborTasks.Clear();
            }

            Logger.DebugS("pathfinding", $"Found route in {cameFrom.Count} tiles");
            // Debugging;  TODO Need to find a cleaner way for this shite
            foreach (var entity in _spawnedTiles)
            {
                if (!entity.Deleted)
                {
                    entity.Delete();
                }
            }
            _spawnedTiles.Clear();
            foreach (var tile in cameFrom.Keys)
            {
                var tileGrid = _mapManager.GetGrid(tile.GridIndex).GridTileToLocal(tile.GridIndices);
                _spawnedTiles.Add(_serverEntityManager.SpawnEntityAt("GreenPathfindTile", tileGrid));
            }

            route = ReconstructPath(cameFrom, currentTile);
            FoundRoute?.Invoke(new PathfindingRoute(route));
            return route;
        }

        private static double DiagonalDistance(TileRef start, TileRef end)
        {
            const double diagonalCost = 1.41;
            int dx = Math.Abs(start.X - end.X);
            int dy = Math.Abs(start.Y - end.Y);
            var result = dx + dy + (diagonalCost - 2 * 1) * Math.Min(dx, dy);
            return result;
        }

        private static double EuclideanDistance(TileRef start, TileRef end)
        {
            int dx = Math.Abs(start.X - end.X);
            int dy = Math.Abs(start.Y - end.Y);
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double ManhattanDistance(TileRef start, TileRef end)
        {
            return 1 * (Math.Abs(start.X - end.X) + Math.Abs(start.Y - end.Y));
        }

        public double GetTileCost(TileRef tile)
        {
            // TODO: This bitch expensive
            // TODO: If you can noclip just return 1

            if (tile.Tile.IsEmpty)
            {
                return 0;
            }

            double cost = 1;

            // If we know there's a known blocker (walls, tablets, etc) here already and it's not dead
            if (_knownBlockers.TryGetValue(tile, out var blocker))
            {
                // If blocker's still in the same spot
                if (!blocker.Entity.Deleted && blocker.Position == blocker.Entity.Transform.GridPosition)
                {
                    return 0;
                }

                // Entity's stale
                _knownBlockers.Remove(tile);
            }

            var gridCoords = _mapManager.GetGrid(tile.GridIndex).GridTileToLocal(tile.GridIndices);
            foreach (var entity in _serverEntityManager.GetEntitiesIntersecting(gridCoords))
            {
                if (!entity.TryGetComponent(out CollidableComponent collidableComponent))
                {
                    continue;
                }

                // It's a wall
                if (collidableComponent.CollisionLayer == 1) {
                    _knownBlockers[tile] = new PathBlocker(entity);
                    return 0;
                }

                // Is table
                if (collidableComponent?.CollisionMask == 19)
                {
                    _knownBlockers[tile] = new PathBlocker(entity);
                    return 0;
                }
                // TODO: Add crates, lockers, for NOW
                // TODO: Add cost stuff here eventually
            }

            return cost;
        }

        private List<TileRef> GetNeighbors(TileRef tileRef, bool allowDiagonals = true)
        {
            List<TileRef> neighbors = new List<TileRef>(8);
            for (int x = -1; x < 2; x++)
            {
                for (int y = -1; y < 2; y++)
                {
                    if (x == 0 & y == 0)
                    {
                        continue;
                    }

                    if (!allowDiagonals && Math.Abs(x) == 1 && Math.Abs(y) == 1)
                    {
                        continue;
                    }

                    var neighborTile = _mapManager
                        .GetGrid(tileRef.GridIndex)
                        .GetTileRef(new MapIndices(tileRef.GridIndices.X + x, tileRef.GridIndices.Y + y));

                    neighbors.Add(neighborTile);
                }
            }

            return neighbors;
        }

        private List<TileRef> ReconstructPath(IDictionary<TileRef, TileRef> cameFrom, TileRef current)
        {
            var result = new List<TileRef> {current};
            TileRef previousCurrent;
            while (cameFrom.ContainsKey(current))
            {
                previousCurrent = current;
                current = cameFrom[current];
                cameFrom.Remove(previousCurrent);
                result.Add(current);
            }

            result.Reverse();
            return result;
        }
    }
}
