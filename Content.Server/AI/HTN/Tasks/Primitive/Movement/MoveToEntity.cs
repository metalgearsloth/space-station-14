using Content.Server.AI.HTN.Tasks.Primitive.Operators.Movement;
using Content.Server.AI.HTN.WorldState;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Primitive.Movement
{
    public sealed class MoveToEntity : PrimitiveTask
    {
        public override string Name => "MoveToEntity";
        public IEntity Target => _target;
        private IEntity _target;

        public MoveToEntity(IEntity owner, IEntity target) : base(owner)
        {
            _target = target;
        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            if (_target == null)
            {
                return false;
            }

            return Owner.Transform.GridID == _target.Transform.GridID;
        }

        public override void SetupOperator()
        {
            TaskOperator = new MoveToEntityOperator(Owner, _target);
        }
    }
}
