using System.Collections.Generic;
using Content.Server.AI.HTN.Tasks.Concrete.Clothing;
using Content.Server.AI.HTN.WorldState;
using Content.Server.GameObjects;
using Content.Shared.GameObjects.Components.Inventory;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Sequence.Clothing
{
    public sealed class EquipBelt : SequenceTask
    {
        public override string Name => "EquipBelt";

        public EquipBelt(IEntity owner) : base(owner)
        {
        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            if (!Owner.TryGetComponent(out InventoryComponent inventoryComponent)) return false;
            return inventoryComponent.GetSlotItem(EquipmentSlotDefines.Slots.BELT) == null;
        }

        /// <inheritdoc />
        public override void SetupSubTasks(AiWorldState context)
        {
            SubTasks = new List<IAiTask>
            {
                new UseClothingInInventory(Owner, EquipmentSlotDefines.Slots.BELT),
                new PickupNearbyClothing(Owner, EquipmentSlotDefines.Slots.BELT),
            };
        }
    }
}
