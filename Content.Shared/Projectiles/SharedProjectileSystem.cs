using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Serialization;

namespace Content.Shared.Projectiles
{
    public abstract class SharedProjectileSystem : EntitySystem
    {
        protected const string ProjectileFixture = "projectile";

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<ProjectileComponent, PreventCollideEvent>(OnProjectilePreventCollide);
            SubscribeLocalEvent<ProjectileComponent, ComponentGetState>(OnProjectileGetState);
            SubscribeLocalEvent<ProjectileComponent, ComponentHandleState>(OnProjectileHandleState);

            SubscribeLocalEvent<RicochetComponent, PreventCollideEvent>(OnRicochetPreventCollide);
            SubscribeLocalEvent<RicochetComponent, ComponentGetState>(OnRicochetGetState);
            SubscribeLocalEvent<RicochetComponent, ComponentHandleState>(OnRicochetHandleState);
        }

        #region Projectile

        public void IgnoreEntity(ProjectileComponent component, EntityUid user)
        {
            if (component.Shooter == user) return;

            component.Shooter = user;
            Dirty(component);
        }

        private void OnProjectileHandleState(EntityUid uid, ProjectileComponent component, ref ComponentHandleState args)
        {
            if (args.Current is not ProjectileComponentState compState) return;

            component.Shooter = compState.Shooter;
        }


        private void OnProjectileGetState(EntityUid uid, ProjectileComponent component, ref ComponentGetState args)
        {
            args.State = new ProjectileComponentState(component.Shooter);
        }

        #endregion

        #region Ricochet

        private void OnRicochetHandleState(EntityUid uid, RicochetComponent component, ref ComponentHandleState args)
        {
            if (args.Current is not RicochetComponentState state) return;
            component.LastRicochet = state.LastRicochet;
            component.Prob = state.Prob;
        }

        private void OnRicochetGetState(EntityUid uid, RicochetComponent component, ref ComponentGetState args)
        {
            args.State = new RicochetComponentState(component.LastRicochet, component.Prob);
        }

        private void OnRicochetPreventCollide(EntityUid uid, RicochetComponent component, ref PreventCollideEvent args)
        {
            if (!args.Cancelled && component.LastRicochet == args.BodyB.Owner)
                args.Cancelled = true;
        }

        private void OnProjectilePreventCollide(EntityUid uid, ProjectileComponent component, ref PreventCollideEvent args)
        {
            if (!args.Cancelled && args.BodyB.Owner == component.Shooter)
                args.Cancelled = true;
        }

        #endregion

        [NetSerializable, Serializable]
        protected sealed class ProjectileComponentState : ComponentState
        {
            public EntityUid? Shooter;

            public ProjectileComponentState(EntityUid? shooter)
            {
                Shooter = shooter;
            }
        }

        [NetSerializable, Serializable]
        protected sealed class RicochetComponentState : ComponentState
        {
            public EntityUid? LastRicochet;
            public float Prob;

            public RicochetComponentState(EntityUid? lastRicochet, float prob)
            {
                LastRicochet = lastRicochet;
                Prob = prob;
            }
        }
    }
}
