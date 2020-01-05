using System;
using Content.Server.AI.HTN.Tasks.Concrete.Operators;
using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Mobs;
using Content.Server.GameObjects.Components.Weapon.Ranged.Hitscan;
using Content.Server.GameObjects.EntitySystems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server.AI.HTN.Tasks.Primitive.Operators.Combat.Ranged
{
    public class ShootLaserAtEntity : IOperator
    {
        private IEntity _owner;
        private IEntity _target;
        public ShootLaserAtEntity(IEntity owner, IEntity target)
        {
            _owner = owner;
            _target = target;
        }
        public Outcome Execute(float frameTime)
        {
            if (!_owner.TryGetComponent(out CombatModeComponent combatModeComponent))
            {
                return Outcome.Failed;
            }

            if (!combatModeComponent.IsInCombatMode)
            {
                combatModeComponent.IsInCombatMode = true;
            }

            if (!_owner.TryGetComponent(out HandsComponent hands) || hands.GetActiveHand == null)
            {
                return Outcome.Failed;
            }

            var laserWeapon = hands.GetActiveHand.Owner;
            laserWeapon.TryGetComponent(out HitscanWeaponComponent hitscanWeapon);

            if (Math.Abs(hitscanWeapon.CapacitorComponent.Charge) < 0.01)
            {
                // Like below, more of a soft fail
                return Outcome.Success;
            }

            if ((_target.Transform.GridPosition.Position - _owner.Transform.GridPosition.Position).Length >
                7.0f) // TODO: use processor range
            {
                // Not necessarily a hard fail, more of a soft fail
                return Outcome.Success;
            }

            var interactionSystem = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<InteractionSystem>();

            interactionSystem.UseItemInHand(_owner, _target.Transform.GridPosition, _target.Uid);
            return Outcome.Success;
        }
    }
}
