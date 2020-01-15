using System.Collections.Generic;
using Content.Server.AI.HTN.Tasks.Primitive.Clothing;
using Content.Server.AI.HTN.WorldState;
using Content.Server.GameObjects;
using Content.Shared.GameObjects.Components.Inventory;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Sequence.Clothing
{
    public sealed class EquipHead : SequenceTask
    {
        public override string Name => "EquipHead";

        public EquipHead(IEntity owner) : base(owner)
        {
        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            if (!Owner.TryGetComponent(out InventoryComponent inventoryComponent)) return false;
            return inventoryComponent.GetSlotItem(EquipmentSlotDefines.Slots.HEAD) == null;
        }

        /// <inheritdoc />
        public override void SetupSubTasks(AiWorldState context)
        {
            SubTasks = new List<IAiTask>
            {
                new UseClothingInInventory(Owner, EquipmentSlotDefines.Slots.HEAD),
                new PickupNearbyClothing(Owner, EquipmentSlotDefines.Slots.HEAD),
            };
        }
    }
}
