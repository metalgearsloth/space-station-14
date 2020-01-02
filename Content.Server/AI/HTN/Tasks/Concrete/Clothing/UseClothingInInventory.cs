using Content.Server.AI.HTN.Tasks.Primitive.Operators.Inventory;
using Content.Server.AI.HTN.WorldState;
using Content.Server.GameObjects;
using Content.Shared.GameObjects.Components.Inventory;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Primitive.Clothing
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
            if (!Owner.TryGetComponent(out HandsComponent handsComponent)) return false;
            // Use it from hands if possible
            foreach (var item in handsComponent.GetAllHeldItems())
            {
                if (!item.Owner.TryGetComponent(out ClothingComponent clothingComponent)) continue;
                if (clothingComponent.SlotFlags == EquipmentSlotDefines.SlotFlags.PREVENTEQUIP ||
                    (clothingComponent.SlotFlags & EquipmentSlotDefines.SlotMasks[_slot]) == 0)
                {
                    continue;
                }
                _clothing = item.Owner;
                return true;
            }

            // TODO: Check backpack

            return false;
        }

        public override void SetupOperator()
        {
            TaskOperator = new UseItemOnPerson(Owner, _clothing);
        }
    }
}
