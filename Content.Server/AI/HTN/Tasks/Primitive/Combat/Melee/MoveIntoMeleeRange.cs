using Content.Server.AI.HTN.Tasks.Primitive.Operators.Movement;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States;
using Content.Server.AI.HTN.WorldState.States.Combat;
using Content.Server.GameObjects.Components.Weapon.Melee;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Primitive.Combat.Melee
{
    public class MoveIntoMeleeRange : PrimitiveTask
    {
        public override string Name => "MoveIntoMeleeRange";
        private IEntity _target;
        private float _maxRange;

        public MoveIntoMeleeRange(IEntity owner) : base(owner)
        {
        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            _target = context.GetStateValue<AttackTarget, IEntity>();
            var weapon = context.GetStateValue<EquippedMeleeWeapon, MeleeWeaponComponent>();
            _maxRange = weapon.Range;

            if (_target == null) return false;
            // Check same grid?
            return true;
        }

        public override void SetupOperator()
        {
            TaskOperator = new MoveToEntityOperator(Owner, _target, _maxRange);
        }
    }
}
