using System.Collections.Generic;
using Content.Server.AI.HTN.Tasks.Primitive.Operators.Inventory;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States.Inventory;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Primitive.Inventory
{
    public class PutEntityInActiveHand : PrimitiveTask
    {
        public override string Name => "PutEntityInActiveHand";
        private IEntity _target;

        public PutEntityInActiveHand(IEntity owner, IEntity target) : base(owner)
        {
            _target = target;
        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            foreach (var item in context.GetStateValue<InventoryState, List<IEntity>>())
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
