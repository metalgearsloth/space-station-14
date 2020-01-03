using System.Collections.Generic;
using Content.Server.GameObjects;
using Content.Shared.GameObjects.Components.Inventory;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.WorldState.States.Inventory
{
    public sealed class Inventory : IStateData
    {
        public string Name => "Inventory";
        private IEntity _owner;

        public IEnumerable<IEntity> Value { get; set; }

        public void Setup(IEntity owner)
        {
            _owner = owner;
        }

        public void Reset()
        {
            Value = GetValue();
        }

        public IEnumerable<IEntity> GetValue()
        {

            if (_owner.TryGetComponent(out HandsComponent handsComponent))
            {
                foreach (var item in handsComponent.GetAllHeldItems())
                {
                    yield return item.Owner;
                }
            }

            if (_owner.TryGetComponent(out InventoryComponent inventoryComponent))
            {
                foreach (var slot in EquipmentSlotDefines.SlotNames)
                {
                    var slotItem = inventoryComponent.GetSlotItem(slot.Key);
                    if (slotItem != null)
                    {
                        yield return slotItem.Owner;
                    }
                }
            }

            // TODO: Storage (backpack etc.)
        }
    }
}
