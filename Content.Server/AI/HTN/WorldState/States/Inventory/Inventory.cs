using System.Collections.Generic;
using Content.Server.GameObjects;
using Content.Shared.GameObjects.Components.Inventory;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.WorldState.States.Inventory
{
    public sealed class Inventory : EnumerableStateData<IEntity>
    {
        public override string Name => "Inventory";

        public override IEnumerable<IEntity> GetValue()
        {

            if (Owner.TryGetComponent(out HandsComponent handsComponent))
            {
                foreach (var item in handsComponent.GetAllHeldItems())
                {
                    yield return item.Owner;
                }
            }

            if (Owner.TryGetComponent(out InventoryComponent inventoryComponent))
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
