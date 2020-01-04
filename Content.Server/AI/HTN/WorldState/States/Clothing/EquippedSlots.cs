using System.Collections.Generic;
using Content.Server.GameObjects;
using Content.Shared.GameObjects.Components.Inventory;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.WorldState.States.Clothing
{
    public class EquippedSlots : EnumerableStateData<EquipmentSlotDefines.Slots>
    {
        public override string Name => "EquippedSlots";

        public override IEnumerable<EquipmentSlotDefines.Slots> GetValue()
        {

            if (!Owner.TryGetComponent(out InventoryComponent inventoryComponent)) yield break;

            foreach (var slot in EquipmentSlotDefines.SlotNames)
            {
                if (inventoryComponent.GetSlotItem(slot.Key) != null)
                {
                    yield return slot.Key;
                }
            }
        }
    }
}
