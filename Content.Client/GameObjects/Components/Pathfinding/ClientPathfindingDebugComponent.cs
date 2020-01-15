using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared.Pathfinding;
using Robust.Client.Graphics.Drawing;
using Robust.Client.Graphics.Overlays;
using Robust.Client.Graphics.Shaders;
using Robust.Client.Interfaces.Graphics.ClientEye;
using Robust.Client.Interfaces.Graphics.Overlays;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Network;
using Robust.Shared.Interfaces.Random;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timers;

namespace Content.Client.GameObjects.Components.Pathfinding
{
    // Component to receive the route data and an overlay to show it
    [RegisterComponent]
    internal sealed class ClientPathfindingDebugComponent : SharedPathfindingDebugComponent
    {
        private DebugPathfindingOverlay _overlay;
        private float _routeDuration = 4.0f; // How long before we remove it from the overlay

        public override void HandleMessage(ComponentMessage message, INetChannel netChannel = null, IComponent component = null)
        {
            base.HandleMessage(message, netChannel, component);
            switch (message)
            {
                case AStarRouteMessage route:
                    ReceivedRoute(route);
                    break;
                case PathfindingGraphMessage graph:
                    ReceivedGraph(graph);
                    break;
            }
        }

        public override void OnAdd()
        {
            base.OnAdd();
            var overlayManager = IoCManager.Resolve<IOverlayManager>();
            _overlay = new DebugPathfindingOverlay();
            overlayManager.AddOverlay(_overlay);
        }

        public override void OnRemove()
        {
            base.OnRemove();
            var overlaymanager = IoCManager.Resolve<IOverlayManager>();
            overlaymanager.RemoveOverlay(nameof(DebugPathfindingOverlay));
        }

        private void ReceivedRoute(AStarRouteMessage routeMessage)
        {
            _overlay.AddRoute(routeMessage);
            Timer.Spawn(TimeSpan.FromSeconds(_routeDuration), () =>
            {
                if (_overlay == null) return;
                _overlay.RemoveRoute(routeMessage);
            });
        }

        private void ReceivedGraph(PathfindingGraphMessage message)
        {
            _overlay.UpdateGraph(message.Graph);
        }
    }

    internal sealed class DebugPathfindingOverlay : Overlay
    {
        // TODO: Add a box like the debug one and show the most recent path stuff
        public override OverlaySpace Space => OverlaySpace.WorldSpace;
        public int Mode { get; private set; } = 1;

        // Graph debugging
        private readonly Dictionary<int, List<Vector2>> _graph = new Dictionary<int, List<Vector2>>();
        private readonly Dictionary<int, Color> _graphColors = new Dictionary<int, Color>();

        // Route debugging
        private readonly List<AStarRouteMessage> _routes = new List<AStarRouteMessage>();

        public DebugPathfindingOverlay() : base(nameof(DebugPathfindingOverlay))
        {
            Shader = IoCManager.Resolve<IPrototypeManager>().Index<ShaderPrototype>("unshaded").Instance();
        }

        public void UpdateGraph(Dictionary<int, List<Vector2>> graph)
        {
            _graph.Clear();
            _graphColors.Clear();
            var robustRandom = IoCManager.Resolve<IRobustRandom>();
            foreach (var (chunk, nodes) in graph)
            {
                _graph[chunk] = nodes;
                _graphColors[chunk] = new Color(robustRandom.NextFloat(), robustRandom.NextFloat(), robustRandom.NextFloat(), 0.1f);
            }
        }

        private void DrawGraph(DrawingHandleWorld worldHandle)
        {
            var viewport = IoCManager.Resolve<IEyeManager>().GetWorldViewport();

            foreach (var (chunk, nodes) in _graph)
            {
                foreach (var tile in nodes)
                {
                    if (!viewport.Contains(tile)) continue;
                    var box = new Box2(
                        tile.X - 0.5f,
                        tile.Y - 0.5f,
                        tile.X + 0.5f,
                        tile.Y + 0.5f);

                    worldHandle.DrawRect(box, _graphColors[chunk]);
                }
            }
        }

        #region pathfinder
        public void AddRoute(AStarRouteMessage routeMessage)
        {
            _routes.Add(routeMessage);
        }

        public void RemoveRoute(AStarRouteMessage routeMessage)
        {
            if (_routes.Contains(routeMessage))
            {
                _routes.Remove(routeMessage);
                return;
            }
            Logger.WarningS("pathfinding", "Not a valid route for overlay removal");
        }

        private void DrawRoutes(DrawingHandleWorld worldHandle)
        {
            var viewport = IoCManager.Resolve<IEyeManager>().GetWorldViewport();

            foreach (var route in _routes)
            {
                var highestgScore = route.GScores.Values.Max();

                foreach (var (tile, score) in route.GScores)
                {
                    if (route.Route.Contains(tile) || !viewport.Contains(tile)) continue;
                    var box = new Box2(
                        tile.X - 0.5f,
                        tile.Y - 0.5f,
                        tile.X + 0.5f,
                        tile.Y + 0.5f);

                    worldHandle.DrawRect(box, new Color(
                        0.0f,
                        score / highestgScore,
                        1.0f - (score / highestgScore),
                        0.1f));
                }

                // Draw box on each tile of route
                foreach (var position in route.Route)
                {
                    if (!viewport.Contains(position)) continue;
                    // worldHandle.DrawLine(position, nextWorld.Value, Color.Blue);
                    var box = new Box2(
                        position.X - 0.5f,
                        position.Y - 0.5f,
                        position.X + 0.5f,
                        position.Y + 0.5f);
                    worldHandle.DrawRect(box, Color.Orange.WithAlpha(0.25f));
                }
                // TODO: Draw remaining stuff
            }
        }
        #endregion

        protected override void Draw(DrawingHandleBase handle)
        {
            var worldHandle = (DrawingHandleWorld) handle;

            if ((Mode & (int) PathfindingDebugMode.Route) != 0)
            {
                DrawRoutes(worldHandle);
            }
        }
    }
}
