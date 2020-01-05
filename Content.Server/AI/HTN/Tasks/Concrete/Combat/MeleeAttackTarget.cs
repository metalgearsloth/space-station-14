using Content.Server.AI.HTN.Tasks.Concrete.Operators;
using Content.Server.AI.HTN.Tasks.Concrete.Operators.Movement;
using Content.Server.AI.HTN.Tasks.Primitive.Operators;
using Content.Server.AI.HTN.Tasks.Primitive.Operators.Combat;
using Content.Server.AI.HTN.Tasks.Primitive.Operators.Movement;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States;
using Content.Server.GameObjects.Components.Weapon.Melee;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Primitive.Combat
{
    public class MeleeAttackTarget : ConcreteTask
    {
        private MoveToEntityOperator _movementHandler;
        private IEntity _target;
        public MeleeAttackTarget(IEntity owner, IEntity target) : base(owner)
        {
            _target = target;
            _movementHandler = new MoveToEntityOperator(Owner, _target);
        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            var equippedWeapon = context.GetStateValue<EquippedMeleeWeapon, MeleeWeaponComponent>();
            return equippedWeapon != null;
        }

        public override void SetupOperator()
        {
            TaskOperator = new SwingMeleeWeaponAtEntity(Owner, _target);
        }

        public override Outcome Execute(float frameTime)
        {
            var movementOutcome = _movementHandler.Execute(frameTime);
            if (movementOutcome != Outcome.Success)
            {
                return movementOutcome;
            }

            var outcome = base.Execute(frameTime);
            return outcome != Outcome.Failed ? Outcome.Continuing : outcome; // Keep on hitting
        }
    }
}
