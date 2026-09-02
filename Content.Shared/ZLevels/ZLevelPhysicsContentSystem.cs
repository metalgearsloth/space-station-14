using Content.Shared.Throwing;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.ZLevels;

/// <summary>
/// Connects authoritative engine z motion to content throwing, landing, and climbable behavior.
/// </summary>
public sealed partial class ZLevelPhysicsContentSystem : EntitySystem
{
    [Dependency] private ZLevelPhysicsSystem _zPhysics = default!;
    [Dependency] private ThrownItemSystem _thrown = default!;
    [Dependency] private INetManager _net = default!;

    [Dependency] private EntityQuery<PhysicsComponent> _physicsQuery = default!;
    [Dependency] private EntityQuery<ZLevelMapComponent> _zMapQuery = default!;

    [SubscribeLocalEvent]
    private void OnThrown(Entity<ZLevelPhysicsComponent> entity, ref ThrownEvent args)
    {
        if (!entity.Comp.Fallable ||
            !entity.Comp.VelocityGravity ||
            entity.Comp.GravityMultiplier <= 0f ||
            !TryComp(entity.Owner, out ThrownItemComponent? thrown) ||
            Transform(entity.Owner).MapUid is not { } mapUid ||
            !_zMapQuery.HasComp(mapUid))
        {
            return;
        }

        // Horizontal throwing remains ordinarily predicted. Only the vertical arc, crossings, and landing wait for
        // the server and replicate through the existing render-pose path.
        thrown.VerticalPhysics = true;
        if (_net.IsClient)
            return;

        Dirty(entity.Owner, thrown);

        var duration = thrown.LandTime - thrown.ThrownTime;
        if (duration is { } flightTime && flightTime > TimeSpan.Zero)
        {
            var launchVelocity = _zPhysics.Gravity *
                                 entity.Comp.GravityMultiplier *
                                 (float) flightTime.TotalSeconds /
                                 2f;
            _zPhysics.SetZVelocity(entity, MathF.Max(entity.Comp.Velocity, launchVelocity));
        }

        _zPhysics.RefreshSupport(entity, true);
        _zPhysics.RefreshBody(entity);
    }

    [SubscribeLocalEvent]
    private void OnLanding(Entity<ZLevelPhysicsComponent> entity, ref ZLevelLandingEvent args)
    {
        if (_net.IsClient || args.Surface != ZLevelImpactSurface.Floor)
            return;

        if (TryComp(entity.Owner, out ThrownItemComponent? thrown) &&
            _physicsQuery.TryComp(entity.Owner, out var physics))
        {
            // This is a floor found by the z solver even when content gravity considers the map weightless.
            _thrown.LandComponent(entity.Owner, thrown, physics, thrown.PlayLandSound, ignoreWeightlessness: true);
            if (thrown.Landed && !thrown.Deleted)
                _thrown.StopThrow(entity.Owner, thrown);
            return;
        }

        var land = new LandEvent(null, true);
        RaiseLocalEvent(entity.Owner, ref land);
    }
}
