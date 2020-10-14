#nullable enable
using Content.Server.GameObjects.EntitySystems.Click;
using Content.Shared.Damage;
using Content.Shared.GameObjects;
using Content.Shared.GameObjects.Components.Damage;
using Content.Shared.Physics;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Components;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Timers;

namespace Content.Server.GameObjects.Components.Projectiles
{
    [RegisterComponent]
    internal class ThrownItemComponent : ProjectileComponent, ICollideBehavior
    {
        private bool _shouldCollide = true;

        public override string Name => "ThrownItem";
        public override uint? NetID => ContentNetIDs.THROWN_ITEM;

        /// <summary>
        ///     User who threw the item.
        /// </summary>
        public IEntity? User;

        void ICollideBehavior.CollideWith(IEntity entity)
        {
            if (!_shouldCollide) return;
            if (entity.TryGetComponent(out PhysicsComponent? collid))
            {
                if (!collid.Hard) // ignore non hard
                    return;

                // Raise an event.
                EntitySystem.Get<InteractionSystem>().ThrowCollideInteraction(User, Owner, entity, Owner.Transform.Coordinates);
            }
            if (entity.TryGetComponent(out IDamageableComponent? damage))
            {
                damage.ChangeDamage(DamageType.Blunt, 10, false, Owner);
            }

            // Stop colliding with mobs, this mimics not having enough velocity to do damage
            // after impacting the first object.
            // For realism this should actually be changed when the velocity of the object is less than a threshold.
            // This would allow ricochets off walls, and weird gravity effects from slowing the object.
            if (Owner.TryGetComponent(out IPhysicsComponent? body) && body.PhysicsShapes.Count >= 1)
            {
                _shouldCollide = false;
            }
        }

        public void StartThrow(Vector2 direction, float speed)
        {
            var comp = Owner.GetComponent<IPhysicsComponent>();
            comp.Status = BodyStatus.InAir;

            var controller = comp.EnsureController<ThrownController>();
            controller.Push(direction, speed);
            Timer.Spawn(200, () =>
            {
                if (Owner.Deleted || !Owner.TryGetComponent(out IPhysicsComponent? physicsComponent)) return;
                physicsComponent.Status = BodyStatus.OnGround;
            });
        }

        public override void Initialize()
        {
            base.Initialize();

            Owner.EnsureComponent<PhysicsComponent>().EnsureController<ThrownController>();
        }
    }
}
