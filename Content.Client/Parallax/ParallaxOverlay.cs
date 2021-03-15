#nullable enable
using Content.Client.GameObjects.EntitySystems;
using Content.Client.Interfaces.Parallax;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client.Parallax
{
    public class ParallaxOverlay : Overlay
    {
        [Dependency] private readonly IEyeManager _eyeManager = default!;
        [Dependency] private readonly IClyde _displayManager = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

        private ParallaxSystem _parallaxSystem = default!;

        public override OverlaySpace Space => OverlaySpace.ScreenSpaceBelowWorld;
        private readonly ShaderInstance _shader;

        public ParallaxOverlay()
        {
            IoCManager.InjectDependencies(this);
            _shader = _prototypeManager.Index<ShaderPrototype>("unshaded").Instance();
            _parallaxSystem = EntitySystem.Get<ParallaxSystem>();
        }

        protected override void Draw(DrawingHandleBase handle, OverlaySpace currentSpace)
        {
            if (!_parallaxSystem.Enabled || _parallaxSystem.Parallax == null) return;

            handle.UseShader(_shader);
            var screenHandle = (DrawingHandleScreen) handle;

            foreach (var layer in _parallaxSystem.Parallax.Layers)
            {
                var (sizeX, sizeY) = layer.ParallaxTexture.Size;
                var (posX, posY) = _eyeManager.ScreenToMap(Vector2.Zero).Position;

                int ox;
                int oy;

                if (layer.Speed > 0.0f)
                {
                    (ox, oy) = (Vector2i) new Vector2(-posX / layer.Speed, posY / layer.Speed);
                    ox = MathHelper.Mod(ox, sizeX);
                    oy = MathHelper.Mod(oy, sizeY);
                }
                else
                {
                    ox = 0;
                    oy = 0;
                }

                var (screenSizeX, screenSizeY) = _displayManager.ScreenSize;
                for (var x = -sizeX; x < screenSizeX; x += sizeX) {
                    for (var y = -sizeY; y < screenSizeY; y += sizeY) {
                        // TODO: Need to handle scaling; use DrawTextureRect
                        screenHandle.DrawTexture(layer.ParallaxTexture, new Vector2(ox + x, oy + y));
                    }
                }
            }
        }
    }
}
