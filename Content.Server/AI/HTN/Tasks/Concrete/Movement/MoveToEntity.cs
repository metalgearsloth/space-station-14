using Content.Server.AI.HTN.Tasks.Concrete.Operators.Movement;
using Content.Server.AI.HTN.Tasks.Primitive;
using Content.Server.AI.HTN.WorldState;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Concrete.Movement
{
    public sealed class MoveToEntity : ConcreteTask
    {

        private IEntity _target;
        public MoveToEntity(IEntity owner, IEntity target) : base(owner)
        {
            _target = target;
        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            return Owner.Transform.GridID == _target.Transform.GridID;
        }

        public override void SetupOperator()
        {
            TaskOperator = new MoveToEntityOperator(Owner, _target);
        }
    }
}
