using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Serialization;

namespace Content.Shared.Visuals;

public sealed class VisualizerCollideSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VisualizerCollideComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<VisualizerCollideComponent, EndCollideEvent>(OnEndCollide);
    }

    public void Refresh(EntityUid ent)
    {
        var contacts = _physics.GetTouchingContacts(ent) - 1;

        if (contacts > 0)
        {
            _appearance.SetData(ent, VisualsCollideState.Key, true);
        }
        else
        {
            _appearance.SetData(ent, VisualsCollideState.Key, false);
        }
    }

    private void OnStartCollide(Entity<VisualizerCollideComponent> ent, ref StartCollideEvent args)
    {
        if (args.OurFixtureId != ent.Comp.Fixture)
            return;

        _appearance.SetData(ent.Owner, VisualsCollideState.Key, true);
    }

    private void OnEndCollide(Entity<VisualizerCollideComponent> ent, ref EndCollideEvent args)
    {
        if (args.OurFixtureId != ent.Comp.Fixture)
            return;

        var contacts = _physics.GetTouchingContacts(args.OurEntity) - 1;

        if (contacts > 0)
            return;

        _appearance.SetData(ent.Owner, VisualsCollideState.Key, false);
    }
}

[Serializable, NetSerializable]
public enum VisualsCollideState : byte
{
    Key,
}
