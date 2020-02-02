using Content.Server.AI.HTN.Tasks.Primitive.Operators;
using Content.Server.AI.HTN.Tasks.Primitive.Operators.Combat.Ranged;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States.Combat;
using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Weapon.Ranged.Hitscan;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Primitive.Combat.Ranged
{
    public class ShootLaserWeapon : PrimitiveTask
    {
        public override string Name => "ShootLaserWeapon";
        private float _attackRange = 7.0f;
        private IEntity _target;
        private HitscanWeaponComponent _equippedWeapon;

        public ShootLaserWeapon(IEntity owner) : base(owner)
        {
        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            _equippedWeapon = (HitscanWeaponComponent) context.GetState<EquippedLaserWeapon, HitscanWeaponComponent>().Value;
            var target = context.GetStateValue<AttackTarget, IEntity>();
            if (target == null)
            {
                return false;
            }

            _target = target;
            return _equippedWeapon != null;
        }

        public override void SetupOperator()
        {
            TaskOperator = new ShootLaserAtEntity(Owner, _target);
        }

        public override Outcome Execute(float frameTime)
        {
            // TODO: Move more of this shit to the operator
            var outcome = base.Execute(frameTime);

            if ((_target.Transform.GridPosition.Position - Owner.Transform.GridPosition.Position).Length >= _attackRange
            )
            {
                return Outcome.Failed;
            }

            if (_target.TryGetComponent(out DamageableComponent damageableComponent))
            {
                if (damageableComponent.IsDead())
                {
                    return Outcome.Success;
                }
            }

            if (outcome != Outcome.Failed)
            {
                // Only succeed when they ded or ammo out
                return Outcome.Continuing;
            }

            return outcome;
        }
    }
}
