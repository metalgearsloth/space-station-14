using Content.Server.AI.HTN.Tasks.Primitive.Operators.Inventory;
using Content.Server.AI.HTN.WorldState;
using Content.Server.GameObjects;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Primitive.Inventory
{
    public class ClearHands : PrimitiveTask
    {
        // TODO: Look at having this put in backpack
        public ClearHands(IEntity owner) : base(owner)
        {
        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            return Owner.HasComponent<HandsComponent>();
        }

        public override void SetupOperator()
        {
            TaskOperator = new DropHandItems(Owner);
        }
    }
}
