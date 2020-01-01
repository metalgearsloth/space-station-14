using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared.GameObjects.Components.Pathfinding;
using Robust.Client.Graphics.Drawing;
using Robust.Client.Graphics.Overlays;
using Robust.Client.Graphics.Shaders;
using Robust.Client.Interfaces.Graphics.ClientEye;
using Robust.Client.Interfaces.Graphics.Overlays;
using Robust.Client.Interfaces.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.Interfaces.Network;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timers;
using Robust.Shared.Utility;

namespace Content.Client.GameObjects.Components.PathfindingComponent
{
    // Component to receive the route data and an overlay to show it
    [RegisterComponent]
    internal sealed class ClientPathfindingSharedComponent : SharedPathfindingComponent
    {
        private DebugPathfindingOverlay _overlay;
        private float _routeDuration = 10.0f; // How long before we remove it from the overlay

        public override void HandleMessage(ComponentMessage message, INetChannel netChannel = null, IComponent component = null)
        {
            base.HandleMessage(message, netChannel, component);
            switch (message)
            {
                case PathfindingRoute route:
                    ReceivedRoute(route);
                    break;
            }
        }

        void ReceivedRoute(PathfindingRoute route)
        {
            _overlay.AddRoute(route);
            Timer.Spawn(TimeSpan.FromSeconds(_routeDuration), () => {_overlay.RemoveRoute(route);});
        }
    }

    internal sealed class DebugPathfindingOverlay : Overlay
    {
        // TODO: Add a box like the debug one and show the most recent path stuff
        public override OverlaySpace Space => OverlaySpace.WorldSpace;

        private readonly List<PathfindingRoute> _routes = new List<PathfindingRoute>();

        public DebugPathfindingOverlay() : base(nameof(DebugPathfindingOverlay))
        {
            Shader = IoCManager.Resolve<IPrototypeManager>().Index<ShaderPrototype>("unshaded").Instance();
        }

        public void AddRoute(PathfindingRoute route)
        {
            _routes.Add(route);
        }

        public void RemoveRoute(PathfindingRoute route)
        {
            if (_routes.Contains(route))
            {
                _routes.Remove(route);
            }
            Logger.WarningS("pathfinding", "Not a valid route for overlay removal");
        }

        protected override void Draw(DrawingHandleBase handle)
        {
            var viewport = IoCManager.Resolve<IEyeManager>().GetWorldViewport();
            var worldHandle = (DrawingHandleWorld) handle;

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
                        1.0f - (score / highestgScore),
                        score / highestgScore,
                        0.25f));
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
    }

    internal sealed class MapChunkOverlay : Overlay
    {
        public override OverlaySpace Space => OverlaySpace.WorldSpace;

        public MapChunkOverlay() : base(nameof(MapChunkOverlay))
        {
            Shader = IoCManager.Resolve<IPrototypeManager>().Index<ShaderPrototype>("unshaded").Instance();
        }

        protected override void Draw(DrawingHandleBase handle)
        {
            var viewport = IoCManager.Resolve<IEyeManager>().GetWorldViewport();
            var worldHandle = (DrawingHandleWorld) handle;
        }
    }
}
