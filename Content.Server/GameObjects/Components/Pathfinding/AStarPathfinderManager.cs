using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Content.Server.Pathfinding;
using JetBrains.Annotations;
using Robust.Server.Interfaces.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Map;

namespace Content.Server.GameObjects.Components.Pathfinding
{
    public interface IPathfinder
    {
        IEnumerable<TileRef> FindPath(TileRef start, TileRef end);
        IEnumerable<TileRef> FindPath(GridCoordinates start, GridCoordinates end);
        double GetTileCost(TileRef tile);
        bool IsTileTraversable(TileRef tile);
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
    public class PathfindingManager : IPathfinder
    {
        // TODO: Handle client requests and return them in a separate pathfinding component

// Ideally you'd store each room on the station and which room(s) it connects to.
// Then you'd be able to get a high-level overview of which rooms you need to go to,
// and you could probably run it asynchronously if the perf is better (connecting path from airlock to airlock).
#pragma warning disable 649
        [Dependency] private readonly IEntityManager _entityManager;
        [Dependency] private readonly IMapManager _mapManager;
        [Dependency] private readonly IServerEntityManager _serverEntityManager;
#pragma warning restore 649

        private float _timeout = 1.0f;
        private readonly IPathfindingPriorityQueue<TileRef> _openTiles = new PathfindingPriorityQueue<TileRef>();
        private readonly IDictionary<TileRef, double> _gScores = new Dictionary<TileRef, double>();
        private readonly IDictionary<TileRef, TileRef> _cameFrom = new Dictionary<TileRef, TileRef>();
        private readonly HashSet<TileRef> _closedTiles = new HashSet<TileRef>();

        // TODO: Add cached paths and interface it with rooms

        // TODO: REMOVE ON PR
        private List<IEntity> _spawnedTiles = new List<IEntity>();
        public event Action<PathfindingRoute> FoundRoute;

        // Anything you can do to speedup performance goes here
        // - Anything that physically blocks movement
        private IDictionary<TileRef, PathBlocker> _knownBlockers = new Dictionary<TileRef, PathBlocker>();
        private IDictionary<TileRef, double> _knownPathCosts = new Dictionary<TileRef, double>();

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
            if (!IsTileTraversable(end))
            {
                return route;
            }

            DateTime pathTimeStart = DateTime.Now;
            // scabbed from https://www.redblobgames.com/pathfinding/a-star/implementation.html#csharp
            // TODO: Look at Sebastian Lague https://www.youtube.com/watch?v=mZfyt03LDH4
            RefreshPaths(); // TODO: Look at optimising this
            _openTiles.Clear();
            _gScores.Clear();
            _closedTiles.Clear();
            _cameFrom.Clear();

            TileRef currentTile = start;
            _openTiles.Enqueue(currentTile, 0);
            _gScores[currentTile] = 0;
            while (_openTiles.Count > 0)
            {
                // TODO: Disabled just for debugging
                //if ((DateTime.Now - pathTimeStart).TotalSeconds > _timeout)
                //{
                //    _openTiles.Clear();
                //    _closedTiles.Clear();
                //    return route;
                //}

                if (currentTile.Equals(end))
                {
                    break;
                }

                currentTile = _openTiles.Dequeue();

                foreach (var next in GetNeighbors(currentTile))
                {
                    if (!IsTileTraversable(currentTile))
                    {
                        continue;
                    }

                    var gScore = _gScores[currentTile] + GetTileCost(next); // TODO: Is the second half of this right?

                    if (!_gScores.ContainsKey(next) || gScore < _gScores[next])
                    {
                        _cameFrom[next] = currentTile;
                        _gScores[next] = gScore;
                        double fScore = _gScores[next] + DiagonalDistance(next, end);
                        _openTiles.Enqueue(next, fScore);

                    }
                }
            }

            Logger.DebugS("pathfinding", $"Found route in {_cameFrom.Count} tiles");
            // Debugging;  TODO Need to find a cleaner way for this shite
            foreach (var entity in _spawnedTiles)
            {
                if (!entity.Deleted)
                {
                    entity.Delete();
                }
            }
            _spawnedTiles.Clear();
            foreach (var tile in _cameFrom.Keys)
            {
                var tileGrid = _mapManager.GetGrid(tile.GridIndex).GridTileToLocal(tile.GridIndices);
                _spawnedTiles.Add(_serverEntityManager.SpawnEntityAt("GreenPathfindTile", tileGrid));
            }

            route = ReconstructPath(_cameFrom, currentTile);
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

        /// <summary>
        /// Will query all pathfinding entities and store their locations
        /// </summary>
        private void RefreshPaths()
        {
            var entityQuery = new TypeEntityQuery(typeof(PathfindingComponent));
            lock (_knownBlockers)
            {
                _knownBlockers.Clear();
                lock (_knownPathCosts)
                {
                    _knownPathCosts.Clear();
                    foreach (var entity in _entityManager.GetEntities(entityQuery))
                    {
                        entity.TryGetComponent(out PathfindingComponent pathfindingComponent);
                        var entityTile = _mapManager.GetGrid(entity.Transform.GridID)
                            .GetTileRef(entity.Transform.GridPosition);
                        if (!pathfindingComponent.Traversable)
                        {
                            _knownBlockers[entityTile] = new PathBlocker(entity);
                            continue;
                        }

                        _knownPathCosts.TryGetValue(entityTile, out var tileCost);

                        _knownPathCosts[entityTile] = tileCost + pathfindingComponent.Cost;
                    }
                }
            }
        }

        public bool IsTileTraversable(TileRef tile)
        {
            if (tile.Tile.IsEmpty)
            {
                return false;
            }

            // If we know there's a known blocker (walls, tablets, etc) here already and it's not dead
            if (_knownBlockers.TryGetValue(tile, out var blocker))
            {
                // If blocker's still in the same spot
                if (!blocker.Entity.Deleted && blocker.Position == blocker.Entity.Transform.GridPosition)
                {
                    return false;
                }

                // Entity's stale
                RefreshPaths();
            }

            return true;
        }

        /// <summary>
        /// Currently it finds out whether a tile is passable or not
        /// It's also slow af.
        /// </summary>
        /// <param name="tile"></param>
        /// <returns></returns>
        public double GetTileCost(TileRef tile)
        {
            // TODO: This bitch expensive
            // TODO: If you can noclip just return 1

            var tileBounds = _mapManager.GetGrid(tile.GridIndex).WorldBounds;
            double cost = 1.0;
            lock (_knownPathCosts)
            {
                // TODO: Should probably store these by GridId
                foreach (var costTile in _knownPathCosts)
                {
                    if (tileBounds.Intersects(_mapManager.GetGrid(costTile.Key.GridIndex).WorldBounds))
                    {
                        cost += costTile.Value;
                    }
                }
            }

            return cost;
        }

        /// <summary>
        /// Get adjacent tiles to this one
        /// </summary>
        /// <param name="tileRef"></param>
        /// <param name="allowDiagonals"></param>
        /// <returns></returns>
        private IEnumerable<TileRef> GetNeighbors(TileRef tileRef, bool allowDiagonals = true)
        {
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

                    yield return neighborTile;
                }
            }
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
