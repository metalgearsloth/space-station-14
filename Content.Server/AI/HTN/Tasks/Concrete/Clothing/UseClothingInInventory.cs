using Content.Server.AI.HTN.Tasks.Concrete.Operators.Inventory;
using Content.Server.AI.HTN.Tasks.Primitive;
using Content.Server.AI.HTN.WorldState;
using Content.Server.GameObjects;
using Content.Shared.GameObjects.Components.Inventory;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Concrete.Clothing
{
    public class UseClothingInInventory : ConcreteTask
    {
        private IEntity _clothing;
        private EquipmentSlotDefines.Slots _slot;

        public UseClothingInInventory(IEntity owner, EquipmentSlotDefines.Slots slot) : base(owner)
        {
            _slot = slot;
        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            foreach (var item in context.GetState<WorldState.States.Inventory.Inventory>().Value)
            {
                if (!item.TryGetComponent(out ClothingComponent clothingComponent)) continue;
                if ((clothingComponent.SlotFlags & EquipmentSlotDefines.SlotMasks[_slot]) != 0) continue;

                _clothing = item;
                break;
            }
            return true;
        }

        public override void SetupOperator()
        {
            TaskOperator = new UseItemOnPerson(Owner, _clothing);
        }
    }
}
