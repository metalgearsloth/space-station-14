using Content.Client.Interfaces.Parallax;
using Robust.Client.Graphics;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client.Parallax
{
    public class ParallaxOverlay : Overlay
    {
        [Dependency] private readonly IParallaxManager _parallaxManager = default!;
        [Dependency] private readonly IEyeManager _eyeManager = default!;
        [Dependency] private readonly IClyde _displayManager = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

        public override bool AlwaysDirty => true;

        public override OverlaySpace Space => OverlaySpace.ScreenSpaceBelowWorld;
        private readonly ShaderInstance _shader;

        public ParallaxOverlay() : base(nameof(ParallaxOverlay))
        {
            IoCManager.InjectDependencies(this);
            _shader = _prototypeManager.Index<ShaderPrototype>("unshaded").Instance();
        }

        protected override void Draw(DrawingHandleBase handle, OverlaySpace currentSpace)
        {
            handle.UseShader(_shader);
            var screenHandle = (DrawingHandleScreen) handle;

            foreach (var layer in _parallaxManager.ParallaxLayers)
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
                        screenHandle.DrawTexture(layer.ParallaxTexture, new Vector2(ox + x, oy + y));
                    }
                }
            }
        }
    }
}
