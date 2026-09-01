using Content.Shared.Audio;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client.Audio;

/// <summary>
/// Debug overlay that shows all ambientsound sources in range
/// </summary>
public sealed class AmbientSoundOverlay : Overlay
{
    private readonly IEntityManager _entManager;
    private readonly AmbientSoundSystem _ambient;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public AmbientSoundOverlay(IEntityManager entManager, AmbientSoundSystem ambient)
    {
        _entManager = entManager;
        _ambient = ambient;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var worldHandle = args.WorldHandle;
        const float Size = 0.25f;
        const float Alpha = 0.25f;

        var query = _entManager.EntityQueryEnumerator<AmbientSoundComponent>();
        while (query.MoveNext(out var ent, out var ambientSound))
        {
            if (!args.TryGetEntityRenderLayer(ent, out var renderLayer) ||
                !args.WorldBounds.Contains(renderLayer.Position))
                continue;

            if (ambientSound.Enabled)
            {
                if (_ambient.IsActive((ent, ambientSound)))
                {
                    worldHandle.DrawCircle(renderLayer.Position, Size, Color.LightGreen.WithAlpha(Alpha * 2f * renderLayer.Opacity));
                }
                else
                {
                    worldHandle.DrawCircle(renderLayer.Position, Size, Color.Orange.WithAlpha(Alpha * renderLayer.Opacity));
                }
            }
            else
            {
                worldHandle.DrawCircle(renderLayer.Position, Size, Color.Red.WithAlpha(Alpha * renderLayer.Opacity));
            }
        }
    }
}
