using System;
using Content.Server.AI.HTN.Tasks.Primitive.Operators;
using Content.Server.AI.HTN.Tasks.Primitive.Operators.Movement;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States.Combat;
using Content.Server.AI.Utils;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Primitive.Combat.Ranged
{
    public class MoveIntoLOS : PrimitiveTask
    {
        public override string Name => "MoveIntoLOS";

        private IEntity _target;
        private bool _inLos = false;

        public MoveIntoLOS(IEntity owner) : base(owner)
        {
        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            _target = context.GetStateValue<AttackTarget, IEntity>();
            return _target != null;
        }

        public override void SetupOperator()
        {
            var taskOp = new MoveToEntityOperator(Owner, _target);
            taskOp.MovedATile += MovedATile;
            TaskOperator = taskOp;
        }

        private void MovedATile()
        {
            _inLos = Visibility.InLineOfSight(Owner, _target);
        }

        public override Outcome Execute(float frameTime)
        {
            if (_inLos)
            {
                return Outcome.Success;
            }

            // So keep moving but every time we hit a new tile we'll chuck a ray out to see if we can see em
            var outcome = base.Execute(frameTime);

            switch (outcome)
            {
                // We're not actually in LOS so we dun goofed
                case Outcome.Success:
                    return Outcome.Failed;
                    break;
                case Outcome.Continuing:
                    return Outcome.Continuing;
                    break;
                case Outcome.Failed:
                    return Outcome.Failed;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
