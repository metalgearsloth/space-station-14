using System.Collections.Generic;
using Content.Server.AI.HTN.Tasks.Primitive.Clothing;
using Content.Server.AI.HTN.WorldState;
using Content.Server.GameObjects;
using Content.Shared.GameObjects.Components.Inventory;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Compound.Clothing
{
    public sealed class EquipBelt : CompoundTask
    {
        public EquipBelt(IEntity owner) : base(owner)
        {
        }

        public override string Name => "EquipBelt";

        public override bool PreconditionsMet(AiWorldState context)
        {
            if (!Owner.TryGetComponent(out InventoryComponent inventoryComponent)) return false;
            return inventoryComponent.GetSlotItem(EquipmentSlotDefines.Slots.BELT) == null;
        }

        public override void SetupMethods(AiWorldState context)
        {
            Methods = new List<IAiTask>
            {
                new UseClothingInInventory(Owner, EquipmentSlotDefines.Slots.BELT),
                new PickupNearbyClothing(Owner, EquipmentSlotDefines.Slots.BELT),
            };
        }
    }
}
