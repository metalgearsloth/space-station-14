using Content.Server.AI.HTN.Tasks.Primitive;
using Content.Server.AI.HTN.Tasks.Primitive.Operators.Inventory;
using Content.Server.AI.HTN.WorldState;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Concrete.Inventory
{
    public class PickupItem : ConcreteTask
    {
        private IEntity _target;
        public PickupItem(IEntity owner, IEntity target) : base(owner)
        {
            _target = target;
        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            // TODO: Check interaction range
            return true;
        }

        public override void SetupOperator()
        {
            TaskOperator = new PickupEntity(Owner, _target);
        }
    }
}
