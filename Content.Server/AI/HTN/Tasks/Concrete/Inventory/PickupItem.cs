using System.Collections.Generic;
using Content.Server.AI.HTN.Tasks.Concrete.Operators.Inventory;
using Content.Server.AI.HTN.Tasks.Primitive;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States.Inventory;
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
            return true;
        }

        public override void ProceduralEffects(in AiWorldState context)
        {
            base.ProceduralEffects(in context);
            var inventory = context.GetStateValue<InventoryState, List<IEntity>>();
            inventory.Add(_target);
        }

        public override void SetupOperator()
        {
            TaskOperator = new PickupEntityOperator(Owner, _target);
        }
    }
}
