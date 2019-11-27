using System;
using System.Collections.Generic;
using Content.Server.Pathfinding;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Server.Interfaces.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Map;

namespace Content.Server.GameObjects.Components.Pathfinding
{
    public interface IPathfinder
    {
        IReadOnlyCollection<TileRef> FindPath(TileRef start, TileRef end, PathfindingDebugMode? debugMode);
    }

    [UsedImplicitly]
    public class AStarPathfinder : IPathfinder
    {
        // Ideally you'd store each room on the station and which room(s) it connects to. Then you'd be able to get a high-level overview of which rooms you need to go to, and you could probably run it asynchronously if the perf is better (connecting path from airlock to airlock).
#pragma warning disable 649
        [Dependency] private readonly IMapManager _mapManager;
        [Dependency] private readonly IServerEntityManager _serverEntityManager;
#pragma warning restore 649

        private float _timeout = 1.0f;
        private readonly IPathfindingPriorityQueue<TileRef> _frontier = new PathfindingPriorityQueue<TileRef>();

        // Debugging
        private List<IEntity> _spawnedEntities;

        public IReadOnlyCollection<TileRef> FindPath(TileRef start, TileRef end, PathfindingDebugMode? debugMode = null)
        {
            DateTime pathTimeStart = DateTime.Now;
            var route = new List<TileRef>();
            // scabbed from https://www.redblobgames.com/pathfinding/a-star/implementation.html#csharp
            // TODO: Look at Sebastian Lague https://www.youtube.com/watch?v=mZfyt03LDH4

            _frontier.Enqueue(start, 0);

            Dictionary<TileRef, TileRef> cameFrom = new Dictionary<TileRef, TileRef>
            {
                {start, start}
            };
            Dictionary<TileRef, double> costSoFar = new Dictionary<TileRef, double>
            {
                {start, 0}
            };
            TileRef currentTile = start;

            while (_frontier.Count > 0)
            {
                if ((DateTime.Now - pathTimeStart).TotalSeconds > _timeout)
                {
                    _frontier.Clear();
                    return route;
                }
                currentTile = _frontier.Dequeue();

                if (currentTile.Equals(end))
                {
                    break;
                }

                var neighbors = GetNeighbors(currentTile);

                foreach (var next in neighbors)
                {
                    var tileCost = TileCost(next);
                    if (tileCost == 0)
                    {
                        continue;
                    }
                    double newCost = costSoFar[currentTile] + tileCost;
                    if (!costSoFar.ContainsKey(next) || newCost < costSoFar[next])
                    {
                        costSoFar[next] = newCost;
                        double priority = newCost + Heurestic(next, end);
                        _frontier.Enqueue(next, priority);
                        cameFrom[next] = currentTile;
                    }
                }
            }

            _frontier.Clear();
            route = ReconstructPath(cameFrom, currentTile);
            if (debugMode != null)
            {
                RunDebug(route, debugMode.Value);
            }

            return route;
        }

        private void RunDebug(IReadOnlyCollection<TileRef> route, PathfindingDebugMode debugMode)
        {
            if (_spawnedEntities != null)
            {
                foreach (var entity in _spawnedEntities)
                {
                    entity?.Delete();
                }
                _spawnedEntities.Clear();
            }

            _spawnedEntities = new List<IEntity>(route.Count);

            switch (debugMode)
            {
                case PathfindingDebugMode.GreenTiles:
                    foreach (var tile in route)
                    {
                        var grid = _mapManager.GetGrid(tile.GridIndex).GridTileToLocal(tile.GridIndices);
                        _spawnedEntities.Add(
                            _serverEntityManager.SpawnEntityAt("GreenPathfindingTile", grid));
                    }
                    break;
                default:
                    break;
            }
        }

        private static double Heurestic(TileRef start, TileRef end)
        {
            return Math.Abs(start.X - end.X) + Math.Abs(start.Y - end.Y);
        }

        private double TileCost(TileRef tile)
        {
            double cost = 1;
            // TODO: Abstract this out with cost profiles. Not sure on the least clusterfuck way to do it.
            if (tile.Tile.IsEmpty)
            {
                return 0;
            }

            var gridCoords = _mapManager.GetGrid(tile.GridIndex).GridTileToLocal(tile.GridIndices);
            foreach (var entity in _serverEntityManager.GetEntitiesIntersecting(gridCoords))
            {
                if (entity.TryGetComponent(out CollidableComponent collidableComponent) &&
                    collidableComponent.CollisionLayer == 1)
                {
                    // It's a wall
                    return 0;
                }
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

        private List<TileRef> ReconstructPath(Dictionary<TileRef, TileRef> cameFrom, TileRef current)
        {
            var result = new List<TileRef> {current};
            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                result.Add(current);
            }
            return result;
        }
    }

    public enum PathfindingDebugMode
    {
        GreenTiles,
    }
}
