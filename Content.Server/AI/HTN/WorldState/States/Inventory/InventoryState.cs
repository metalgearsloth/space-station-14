using System.Collections.Generic;
using Content.Server.GameObjects;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.WorldState.States.Inventory
{
    public sealed class InventoryState : EnumerableStateData<IEntity>
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

            /* TODO: Get this working by not checking pockets unless available
            if (Owner.TryGetComponent(out InventoryComponent inventoryComponent))
            {
                foreach (var slot in EquipmentSlotDefines.SlotNames)
                {
                    if (inventoryComponent.TryGetSlotItem(slot.Key, out ItemComponent slotItem))
                    {
                        yield return slotItem.Owner;
                    }
                }
            }
            */

            // TODO: Storage (backpack etc.)
        }
    }
}
