using Content.Server.AI.HTN.WorldState;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Primitive.Movement
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
            // TODO: CanMove
            return true;
        }

        public override void SetupOperator()
        {
            TaskOperator = new Operators.Movement.MoveToEntity(Owner, _target);
        }
    }
}
