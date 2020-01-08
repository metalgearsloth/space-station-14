using Content.Server.AI.HTN.WorldState;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.AI.HTN.Tasks.Primitive.Movement
{
    public sealed class MoveToGrid : ConcreteTask
    {
        private GridCoordinates _target;
        public MoveToGrid(IEntity owner, GridCoordinates target) : base(owner)
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
            TaskOperator = new Concrete.Operators.Movement.MoveToGrid(Owner, _target);
        }
    }

}
