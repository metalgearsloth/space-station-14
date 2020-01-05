using Content.Server.AI.HTN.Tasks.Concrete.Operators.Inventory;
using Content.Server.AI.HTN.Tasks.Primitive;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States.Inventory;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Concrete.Inventory
{
    public class PutEntityInActiveHand : ConcreteTask
    {
        private IEntity _target;

        public PutEntityInActiveHand(IEntity owner, IEntity target) : base(owner)
        {
            _target = target;
        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            foreach (var item in context.GetEnumerableStateValue<InventoryState, IEntity>())
            {
                if (item == _target)
                {
                    return true;
                }
            }

            return false;
        }

        public override void SetupOperator()
        {
            TaskOperator = new PutEntityInActiveHandOperator(Owner, _target);
        }
    }
}
