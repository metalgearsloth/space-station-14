using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using Lidgren.Network;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.Interfaces.Network;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Server.Pathfinding
{
    [UsedImplicitly]
    [RegisterComponent]
    public class AStarPathfinder :  Component
    {
#pragma warning disable 649
        [Dependency] private readonly IMapManager _mapManager;
#pragma warning restore 649

        public override string Name => "Pathfinder";
        private float _timeout = 1.0f;
        private Queue _pathfinds = new Queue();
        private bool _processing = false;

        private Dictionary<List<TileRef>, List<TileRef>> _cache;

        public override void Initialize()
        {
            base.Initialize();
            _cache = new Dictionary<List<TileRef>, List<TileRef>>();
        }

        public override void HandleMessage(ComponentMessage message, INetChannel netChannel = null, IComponent component = null)
        {
            base.HandleMessage(message, netChannel, component);

            switch (message)
            {
                case PathfindRequestMessage msg:
                    AddPathfind(msg.Start, msg.End);
                    break;
            }
        }

        public List<TileRef> FindPath(GridCoordinates start, GridCoordinates end)
        {
            _processing = true;
            // scabbed from https://www.redblobgames.com/pathfinding/a-star/implementation.html#csharp
            var frontier = new PathfindingPriorityQueue<TileRef>();
            TileRef startTile = _mapManager.GetGrid(start.GridID).GetTileRef(start);
            TileRef endTile = _mapManager.GetGrid(start.GridID).GetTileRef(end);

            List<TileRef> cacheResults = new List<TileRef> {startTile, endTile};
            if (_cache.ContainsKey(cacheResults))
            {
                _processing = false;
                return _cache[cacheResults];
            }

            frontier.Enqueue(startTile, 0);

            Dictionary<TileRef, TileRef> cameFrom = new Dictionary<TileRef, TileRef>
            {
                {startTile, startTile}
            };
            Dictionary<TileRef, double> costSoFar = new Dictionary<TileRef, double>
            {
                {startTile, 0}
            };
            TileRef currentTile = startTile;

            while (frontier.Count > 0)
            {
                currentTile = frontier.Dequeue();

                if (currentTile.Equals(endTile))
                {
                    break;
                }

                GridCoordinates current = _mapManager.GetGrid(start.GridID).GridTileToLocal(currentTile.GridIndices);
                var neighbours = Neighbours(current);

                foreach (var next in neighbours)
                {
                    double newCost = costSoFar[currentTile] + TileCost(next);
                    if (!costSoFar.ContainsKey(next) || newCost < costSoFar[next])
                    {
                        costSoFar[next] = newCost;
                        double priority = newCost + Heurestic(next, endTile);
                        frontier.Enqueue(next, priority);
                        cameFrom[next] = currentTile;
                    }
                }
            }

            List<TileRef> route = ReconstructPath(cameFrom, currentTile);
            _cache.Add(new List<TileRef>{startTile, endTile}, route);

            _processing = false;
            return route;
        }

        public void OnUpdate(float frameTime)
        {
            if (_processing == false && _pathfinds.Count > 0)
            {
                List<GridCoordinates> thing = (List<GridCoordinates>)_pathfinds.Dequeue();
                FindPath(thing[0], thing[1]);
            }
            return;
        }

        private void AddPathfind(GridCoordinates start, GridCoordinates end)
        {
            _pathfinds.Enqueue(new List<GridCoordinates>{
                start, end
            });
            return;
        }

        private static double Heurestic(TileRef start, TileRef end)
        {
            return Math.Abs(start.X - end.X) + Math.Abs(start.Y - end.Y);
        }

        private int TileCost(TileRef tile)
        {
            // TODO: Potentially abstract this out
            if (tile.Tile.IsEmpty)
            {
                return 0;
            }
            // TODO: Get walls on tile and add 0
            return 1;
        }

        private List<TileRef> Neighbours(GridCoordinates grid)
        {
            TileRef tile = _mapManager.GetGrid(grid.GridID).GetTileRef(grid);
            MapIndices nwIndex = new MapIndices(tile.X - 1, tile.Y + 1);
            MapIndices nIndex = new MapIndices(tile.X, tile.Y + 1);
            MapIndices neIndex = new MapIndices(tile.X + 1, tile.Y + 1);

            MapIndices wIndex = new MapIndices(tile.X - 1, tile.Y);
            MapIndices eIndex = new MapIndices(tile.X + 1, tile.Y);

            MapIndices swIndex = new MapIndices(tile.X - 1, tile.Y - 1);
            MapIndices sIndex = new MapIndices(tile.X, tile.Y - 1);
            MapIndices seIndex = new MapIndices(tile.X + 1, tile.Y - 1);

            _mapManager.GetGrid(grid.GridID).TryGetTileRef(nwIndex, out TileRef nw);
            _mapManager.GetGrid(grid.GridID).TryGetTileRef(nIndex, out TileRef n);
            _mapManager.GetGrid(grid.GridID).TryGetTileRef(neIndex, out TileRef ne);

            _mapManager.GetGrid(grid.GridID).TryGetTileRef(wIndex, out TileRef w);
            _mapManager.GetGrid(grid.GridID).TryGetTileRef(eIndex, out TileRef e);

            _mapManager.GetGrid(grid.GridID).TryGetTileRef(swIndex, out TileRef sw);
            _mapManager.GetGrid(grid.GridID).TryGetTileRef(sIndex, out TileRef s);
            _mapManager.GetGrid(grid.GridID).TryGetTileRef(seIndex, out TileRef se);

            return new List<TileRef>
            {
                nw,
                n,
                ne,
                w,
                e,
                sw,
                s,
                se,
            };
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

        [Serializable, NetSerializable]
        public class PathfindRequestMessage : ComponentMessage
        {
            public readonly GridCoordinates Start;
            public readonly GridCoordinates End;

            public PathfindRequestMessage(GridCoordinates start, GridCoordinates end)
            {
                Start = start;
                End = end;
            }
        }

        [Serializable, NetSerializable]
        public class PathfindResultMessage : ComponentMessage
        {
            public readonly List<TileRef> Route;

            public PathfindResultMessage(List<TileRef> route)
            {
                Route = route;
            }
        }
    }
}
