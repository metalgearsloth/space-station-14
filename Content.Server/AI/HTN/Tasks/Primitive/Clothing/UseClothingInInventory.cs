using System.Collections.Generic;
using Content.Server.AI.HTN.Tasks.Primitive.Operators.Inventory;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States.Inventory;
using Content.Server.GameObjects;
using Content.Shared.GameObjects.Components.Inventory;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Primitive.Clothing
{
    public class UseClothingInInventory : PrimitiveTask
    {
        private IEntity _clothing;
        private EquipmentSlotDefines.Slots _slot;

        public UseClothingInInventory(IEntity owner, EquipmentSlotDefines.Slots slot) : base(owner)
        {
            _slot = slot;
        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            foreach (var item in context.GetStateValue<InventoryState, List<IEntity>>())
            {
                if (!item.TryGetComponent(out ClothingComponent clothingComponent)) continue;
                if ((clothingComponent.SlotFlags & EquipmentSlotDefines.SlotMasks[_slot]) == 0) continue;

                _clothing = item;
                return true;
            }
            return false;
        }

        public override void SetupOperator()
        {
            TaskOperator = new UseItemOnPerson(Owner, _clothing);
        }
    }
}
