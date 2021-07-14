using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Robust.Server.Physics;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Physics;

namespace Content.Server.Shuttles
{
    internal sealed class ShuttleSystem : EntitySystem
    {
        private const float TileMassMultiplier = 1f;

        // TODO: Replace with thrusters
        private const float SpeedRatio = 160.0f;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<ShuttleComponent, ComponentStartup>(HandleShuttleStartup);
            SubscribeLocalEvent<ShuttleComponent, ComponentShutdown>(HandleShuttleShutdown);

            SubscribeLocalEvent<GridInitializeEvent>(HandleGridInit);
            SubscribeLocalEvent<GridFixtureChangeEvent>(HandleGridFixtureChange);
        }

        private void HandleGridFixtureChange(GridFixtureChangeEvent args)
        {
            var fixture = args.NewFixture;

            if (fixture == null) return;

            fixture.Mass = fixture.Area * TileMassMultiplier;

            if (fixture.Body.Owner.TryGetComponent(out ShuttleComponent? shuttleComponent))
            {
                // TODO: Suss out something better than this.
                shuttleComponent.SpeedMultipler = fixture.Body.Mass / SpeedRatio;
            }
        }

        private void HandleGridInit(GridInitializeEvent ev)
        {
            EntityManager.GetEntity(ev.EntityUid).EnsureComponent<ShuttleComponent>();
        }

        private void HandleShuttleStartup(EntityUid uid, ShuttleComponent component, ComponentStartup args)
        {
            if (!component.Owner.TryGetComponent(out IMapGridComponent? mapGridComp))
            {
                return;
            }

            if (!component.Owner.TryGetComponent(out PhysicsComponent? physicsComponent))
            {
                return;
            }

            if (component.Enabled)
            {
                Enable(physicsComponent);
            }

            if (component.Owner.TryGetComponent(out ShuttleComponent? shuttleComponent))
            {
                // TODO: Suss out something better than this.
                shuttleComponent.SpeedMultipler = physicsComponent.Mass / SpeedRatio;
            }
        }

        public void Toggle(ShuttleComponent component)
        {
            if (!component.Owner.TryGetComponent(out PhysicsComponent? physicsComponent)) return;

            component.Enabled = !component.Enabled;

            if (component.Enabled)
            {
                Enable(physicsComponent);
            }
            else
            {
                Disable(physicsComponent);
            }
        }

        private void Enable(PhysicsComponent component)
        {
            component.BodyType = BodyType.Dynamic;
            component.BodyStatus = BodyStatus.InAir;
            //component.FixedRotation = false; TODO WHEN ROTATING SHUTTLES FIXED.
            component.FixedRotation = true;
            component.LinearDamping = 0.05f;
        }

        private void Disable(PhysicsComponent component)
        {
            component.BodyType = BodyType.Static;
            component.BodyStatus = BodyStatus.OnGround;
            component.FixedRotation = true;
        }

        private void HandleShuttleShutdown(EntityUid uid, ShuttleComponent component, ComponentShutdown args)
        {
            if (!component.Owner.TryGetComponent(out PhysicsComponent? physicsComponent))
            {
                return;
            }

            Disable(physicsComponent);

            foreach (var fixture in physicsComponent.Fixtures)
            {
                fixture.Mass = 0f;
            }
        }
    }
}