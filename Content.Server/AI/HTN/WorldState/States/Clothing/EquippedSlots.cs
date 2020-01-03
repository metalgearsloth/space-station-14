using System.Collections.Generic;
using Content.Server.GameObjects;
using Content.Shared.GameObjects.Components.Inventory;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.WorldState.States.Clothing
{
    public class EquippedSlots
    {
        public string Name => "EquippedSlots";
        private IEntity _owner;

        public IEnumerable<EquipmentSlotDefines.Slots> Value { get; set; }

        public void Setup(IEntity owner)
        {
            _owner = owner;
        }

        public IEnumerable<EquipmentSlotDefines.Slots> GetValue()
        {

            if (!_owner.TryGetComponent(out InventoryComponent inventoryComponent)) yield break;

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
