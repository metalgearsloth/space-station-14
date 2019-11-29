using System.Collections.Generic;
using Robust.Client.Graphics.Drawing;
using Robust.Client.Graphics.Overlays;
using Robust.Client.Graphics.Shaders;
using Robust.Client.Interfaces.Graphics.ClientEye;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client.GameObjects.Components.PathfindingComponent
{
    // TODO: Finish me
    internal sealed class PathfindingOverlay : Overlay
    {
        private readonly IComponentManager _componentManager;
        private readonly IEyeManager _eyeManager;
        private readonly IPrototypeManager _prototypeManager;
        private readonly IMapManager _mapManager;
        private Stack<List<TileRef>> _routes = new Stack<List<TileRef>>();

        public override OverlaySpace Space => OverlaySpace.WorldSpace;

        public PathfindingOverlay(IComponentManager compMan, IEyeManager eyeMan, IPrototypeManager protoMan)
            : base(nameof(PathfindingOverlay))
        {
            _componentManager = compMan;
            _eyeManager = eyeMan;
            _prototypeManager = protoMan;

            Shader = _prototypeManager.Index<ShaderPrototype>("unshaded").Instance();

        }

        protected override void Draw(DrawingHandleBase handle)
        {
            var worldHandle = (DrawingHandleWorld) handle;

            var viewport = _eyeManager.GetWorldViewport();
            foreach (var route in _routes)
            {
                foreach (var tile in route)
                {
                    var worldPosition = _mapManager.GetGrid(tile.GridIndex).WorldPosition;
                    var tileBox = new Box2(new Vector2(worldPosition.X - 0.5f, worldPosition.Y - 0.5f), new Vector2(worldPosition.X + 0.5f, worldPosition.Y + 0.5f));
                    worldHandle.DrawRect(tileBox, Color.Green, false);
                }
            }
        }
    }
}
