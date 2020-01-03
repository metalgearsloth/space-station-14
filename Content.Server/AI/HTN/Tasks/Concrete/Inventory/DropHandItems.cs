using Content.Server.AI.HTN.Tasks.Primitive;
using Content.Server.AI.HTN.Tasks.Primitive.Operators.Inventory;
using Content.Server.AI.HTN.WorldState;
using Content.Server.GameObjects;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Concrete.Inventory
{
    public class DropHandItems : ConcreteTask
    {
        // TODO: Look at having this put in backpack
        public DropHandItems(IEntity owner) : base(owner)
        {
        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            return Owner.HasComponent<HandsComponent>();
        }

        public override void SetupOperator()
        {
            TaskOperator = new Primitive.Operators.Inventory.DropHandItems(Owner);
        }
    }
}
