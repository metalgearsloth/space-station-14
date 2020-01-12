using System;
using System.Collections.Generic;
using Content.Server.GameObjects.EntitySystems.Pathfinding;
using Content.Server.GameObjects.EntitySystems.Pathfinding.Pathfinders;
using Content.Shared.Pathfinding;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Maths;

namespace Content.Server.GameObjects.Components.Pathfinding
{
    [RegisterComponent]
    public sealed class ServerPathfindingDebugDebugComponent : SharedPathfindingDebugComponent
    {
        protected override void Startup()
        {
            base.Startup();
            HandleMode(Mode);
        }

        protected override void Shutdown()
        {
            base.Shutdown();
            HandleMode(PathfindingDebugMode.None);
        }

        private void HandleMode(PathfindingDebugMode mode)
        {
            var pathfinder = IoCManager.Resolve<IPathfinder>();
            var pathfinderSystem = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<PathfindingSystem>();
            if (Mode == PathfindingDebugMode.Route && mode != PathfindingDebugMode.Route)
            {
                pathfinder.DebugRoute -= RouteDebug;
            }

            switch (mode)
            {
                case PathfindingDebugMode.None:
                    break;
                case PathfindingDebugMode.Route:
                    pathfinder.DebugRoute += RouteDebug;
                    break;
                case PathfindingDebugMode.ConsideredTiles:
                    break;
                case PathfindingDebugMode.Graph:
                    var gridId = Owner.Transform.GridID;
                    var grid = IoCManager.Resolve<IMapManager>().GetGrid(gridId);
                    var chunks = pathfinderSystem.GetChunks(gridId);
                    var graph = new Dictionary<int, List<Vector2>>();


                    for (var i = 0; i < chunks.Count; i++)
                    {
                        var tiles = new List<Vector2>();
                        var chunk = chunks[i];
                        foreach (var node in chunk.GetNodes())
                        {
                            var vec = grid.GridTileToLocal(node.TileRef.GridIndices);
                            tiles.Add(vec.Position);
                        }
                        graph.Add(i, tiles);
                    }
                    SendNetworkMessage(new PathfindingGraphMessage(graph));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        // TODO: Look at how SnapGrid does indexing to be F A S T
        // TODO: If client HandleMessage comes through then send them the graph for grids
        private void RouteDebug(PathfindingRoute route)
        {
            SendNetworkMessage(route);
        }
    }
}
