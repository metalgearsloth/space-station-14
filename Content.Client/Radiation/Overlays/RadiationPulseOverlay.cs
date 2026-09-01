using System.Numerics;
using System.Linq;
using Content.Shared.Radiation.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Radiation.Overlays
{
    public sealed partial class RadiationPulseOverlay : Overlay
    {
        private static readonly ProtoId<ShaderPrototype> RadiationShader = "Radiation";

        [Dependency] private IEntityManager _entityManager = default!;
        [Dependency] private IPrototypeManager _prototypeManager = default!;
        [Dependency] private IGameTiming _gameTiming = default!;

        public override OverlaySpace Space => OverlaySpace.WorldSpace;
        public override bool RequestScreenTexture => true;

        private readonly ShaderInstance _baseShader;
        private readonly Dictionary<EntityUid, (ShaderInstance shd, RadiationShaderInstance instance)> _pulses = new();

        public RadiationPulseOverlay()
        {
            IoCManager.InjectDependencies(this);
            _baseShader = _prototypeManager.Index(RadiationShader).Instance().Duplicate();
        }

        protected override bool BeforeDraw(in OverlayDrawArgs args)
        {
            RadiationQuery();
            foreach (var pulse in _pulses.Keys)
            {
                if (args.TryGetEntityRenderLayer(pulse, out _))
                    return true;
            }

            return false;
        }

        protected override void Draw(in OverlayDrawArgs args)
        {
            if (ScreenTexture == null)
                return;

            var worldHandle = args.WorldHandle;
            var viewport = args.Viewport;

            foreach (var (pulseEntity, value) in _pulses)
            {
                var (shd, instance) = value;
                if (!args.TryGetEntityRenderLayer(pulseEntity, out var renderLayer))
                    continue;

                // To be clear, this needs to use "inside-viewport" pixels.
                // In other words, specifically NOT IViewportControl.WorldToScreen (which uses outer coordinates).
                var tempCoords = viewport.WorldToLocal(renderLayer.Position);
                tempCoords.Y = viewport.Size.Y - tempCoords.Y;
                shd?.SetParameter("renderScale", viewport.RenderScale);
                shd?.SetParameter("positionInput", tempCoords);
                shd?.SetParameter("range", instance.Range);
                var life = (_gameTiming.RealTime - instance.Start).TotalSeconds / instance.Duration;
                shd?.SetParameter("life", (float)life);

                // There's probably a very good reason not to do this.
                // Oh well!
                shd?.SetParameter("SCREEN_TEXTURE", viewport.RenderTarget.Texture);

                worldHandle.UseShader(shd);
                worldHandle.DrawRect(
                    Box2.CenteredAround(renderLayer.Position, new Vector2(instance.Range, instance.Range) * 2f),
                    Color.White.WithAlpha(renderLayer.Opacity));
            }

            worldHandle.UseShader(null);
        }

        // Keep one shader per pulse in PVS. Layer ownership and projection are selected by OverlayDrawArgs so
        // pulses on visible lower maps are not discarded just because they are not on the controlling eye map.
        private void RadiationQuery()
        {
            var pulses = _entityManager.EntityQueryEnumerator<RadiationPulseComponent>();
            while (pulses.MoveNext(out var pulseEntity, out var pulse))
            {
                if (!_pulses.ContainsKey(pulseEntity))
                {
                    _pulses.Add(
                            pulseEntity,
                            (
                                _baseShader.Duplicate(),
                                new RadiationShaderInstance(
                                    pulse.VisualRange,
                                    pulse.StartTime,
                                    pulse.VisualDuration
                                )
                            )
                    );
                }
            }

            foreach (var pulseEntity in _pulses.Keys.ToArray())
            {
                if (_entityManager.EntityExists(pulseEntity) &&
                    _entityManager.TryGetComponent(pulseEntity, out RadiationPulseComponent? pulse) &&
                    pulse is { } pulseComp)
                {
                    var shaderInstance = _pulses[pulseEntity];
                    shaderInstance.instance.Range = pulseComp.VisualRange;
                }
                else
                {
                    _pulses[pulseEntity].shd.Dispose();
                    _pulses.Remove(pulseEntity);
                }
            }

        }
        private sealed record RadiationShaderInstance(float Range, TimeSpan Start, float Duration)
        {
            public float Range = Range;
            public TimeSpan Start = Start;
            public float Duration = Duration;
        };
    }
}

