using Content.Server.AI.HTN.Tasks.Concrete.Operators.Combat.Ranged.Laser;
using Content.Server.AI.HTN.Tasks.Primitive;
using Content.Server.AI.HTN.WorldState;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Concrete.Inventory
{
    public class UseItemOnEntity : ConcreteTask
    {
        private IEntity _equippedItem;
        private IEntity _useTarget;

        public UseItemOnEntity(IEntity owner, IEntity equippedItem, IEntity useTarget) : base(owner)
        {
            _equippedItem = equippedItem;
            _useTarget = useTarget;
        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            // TODO: Range check
            return true;
        }

        public override void SetupOperator()
        {
            TaskOperator = new UseItemOnEntityOperator(Owner, _equippedItem, _useTarget);
        }
    }
}
