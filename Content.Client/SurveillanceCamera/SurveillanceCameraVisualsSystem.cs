using Content.Shared.SurveillanceCamera;
using Content.Shared.Visuals;
using Robust.Client.GameObjects;

namespace Content.Client.SurveillanceCamera;

public sealed class SurveillanceCameraVisualsSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SurveillanceCameraVisualsComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(EntityUid uid, SurveillanceCameraVisualsComponent component,
        ref AppearanceChangeEvent args)
    {
        if (!args.AppearanceData.TryGetValue(SurveillanceCameraVisualsKey.Key, out var data)
            || data is not SurveillanceCameraVisuals key
            || args.Sprite == null
            || !args.Sprite.LayerMapTryGet(SurveillanceCameraVisualsKey.Layer, out int layer))
        {
            return;
        }

        if (HasComp<VisualizerCollideComponent>(uid) && args.AppearanceData.TryGetValue(VisualsCollideState.Key, out var collide))
        {
            if ((bool)collide)
            {
                key = SurveillanceCameraVisuals.InUse;
            }
        }

        if (!component.CameraSprites.TryGetValue(key, out var state))
        {
            return;
        }

        args.Sprite.LayerSetState(layer, state, resetAnimation: false);
    }
}
