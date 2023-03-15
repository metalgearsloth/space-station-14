using Content.Server.GameTicking.Events;
using Content.Shared.Friction;
using Robust.Shared.Physics.Controllers;
using Robust.Shared.Physics.Dynamics;

namespace Content.Server.Movement.Systems;

public sealed class AprilFoolsSystem : EntitySystem
{
    [Dependency] private readonly Gravity2DController _physics = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStart);
    }

    private void OnRoundStart(RoundStartingEvent ev)
    {
        EntityManager.System<TileFrictionController>().Enabled = false;

        foreach (var map in EntityQuery<PhysicsMapComponent>(true))
        {
            _physics.SetGravity(map.Owner, new Vector2(0f, -4.9f));
        }
    }
}
