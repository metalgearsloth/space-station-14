using Content.Server.AI.HTN.Tasks.Primitive.Operators.Movement;
using Content.Server.AI.HTN.WorldState;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.AI.HTN.Tasks.Primitive.Combat.Ranged
{
    public class MoveIntoLOS : PrimitiveTask
    {
        public override string Name => "MoveIntoLOS";
        private GridCoordinates _firingSpot;

        public MoveIntoLOS(IEntity owner) : base(owner)
        {
        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            // TODO: get nearest valid firing position to us (need to cast a ray from tile centre)
            // Check same grid?
            return true;
        }

        public override void SetupOperator()
        {
            TaskOperator = new MoveToGridOperator(Owner, _firingSpot);
        }
    }
}
