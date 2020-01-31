using Content.Server.AI.HTN.Tasks.Primitive.Operators.Movement;
using Content.Server.AI.HTN.WorldState;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.AI.HTN.Tasks.Primitive.Movement
{
    public sealed class MoveToGrid : PrimitiveTask
    {
        public override string Name => "MoveToGrid";

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
            TaskOperator = new MoveToGridOperator(Owner, _target);
        }
    }

}
