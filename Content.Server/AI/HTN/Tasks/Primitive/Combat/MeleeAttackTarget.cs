using Content.Server.AI.HTN.Tasks.Primitive.Operators;
using Content.Server.AI.HTN.Tasks.Primitive.Operators.Combat;
using Content.Server.AI.HTN.Tasks.Primitive.Operators.Movement;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Primitive.Combat
{
    public class MeleeAttackTarget : PrimitiveTask
    {
        private MoveToEntity _movementHandler;
        private IEntity _target;
        public MeleeAttackTarget(IEntity owner, IEntity target) : base(owner)
        {
            _target = target;
            _movementHandler = new MoveToEntity(Owner, _target);
        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            var equippedWeapon = context.GetState<EquippedMeleeWeapon>().GetValue();
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
