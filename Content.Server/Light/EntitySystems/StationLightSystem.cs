using Content.Server.Station.Components;
using Content.Server.Station.Events;
using Content.Shared.Light.Components;
using Robust.Shared.Map.Components;

namespace Content.Server.Light.EntitySystems;

/// <summary>
/// <see cref="StationLightComponent"/>
/// </summary>
public sealed class StationLightSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationLightComponent, StationPostInitEvent>(OnStationLightPostInit);
    }

    private void OnStationLightPostInit(Entity<StationLightComponent> ent, ref StationPostInitEvent args)
    {
        if (!TryComp(ent.Owner, out StationDataComponent? sData))
            return;

        foreach (var grid in sData.Grids)
        {
            if (!EntityManager.TransformQuery.TryComp(grid, out var xform) || xform.MapUid == null)
                continue;

            var light = EnsureComp<MapLightComponent>(xform.MapUid.Value);
            light.AmbientLightColor = ent.Comp.Color;
            Dirty(xform.MapUid.Value, light);
        }
    }
}
