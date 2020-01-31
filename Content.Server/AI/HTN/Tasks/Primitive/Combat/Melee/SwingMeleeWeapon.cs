using Content.Server.AI.HTN.Tasks.Primitive.Operators;
using Content.Server.AI.HTN.Tasks.Primitive.Operators.Combat;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States;
using Content.Server.AI.HTN.WorldState.States.Combat;
using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Weapon.Melee;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Primitive.Combat.Melee
{
    public class SwingMeleeWeapon : PrimitiveTask
    {
        public override string Name => "SwingMeleeWeapon";
        private float? _attackRange;
        private IEntity _target;

        public SwingMeleeWeapon(IEntity owner) : base(owner)
        {
        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            var equippedWeapon = (MeleeWeaponComponent) context.GetState<EquippedMeleeWeapon, MeleeWeaponComponent>().Value;
            var target = context.GetStateValue<AttackTarget, IEntity>();
            if (target == null)
            {
                return false;
            }

            _target = target;
            _attackRange = equippedWeapon?.Range;
            return equippedWeapon != null;
        }

        public override void SetupOperator()
        {
            TaskOperator = new SwingMeleeWeaponAtEntity(Owner, _target);
        }

        public override Outcome Execute(float frameTime)
        {
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

            if (!Owner.TryGetComponent(out HandsComponent handsComponent)) return Outcome.Failed;

            if (handsComponent.GetActiveHand == null || !handsComponent.GetActiveHand.Owner.HasComponent<MeleeWeaponComponent>())
            {
                return Outcome.Failed;
            }

            if (outcome != Outcome.Failed)
            {
                // Only succeed when they ded
                return Outcome.Continuing;
            }


            return outcome;
        }
    }
}
