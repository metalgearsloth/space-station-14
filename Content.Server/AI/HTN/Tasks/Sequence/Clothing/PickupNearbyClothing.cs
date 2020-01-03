using Content.Server.AI.HTN.Tasks.Concrete.Inventory;
using Content.Server.AI.HTN.Tasks.Concrete.Movement;
using Content.Server.AI.HTN.Tasks.Primitive.Movement;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States.Clothing;
using Content.Server.AI.HTN.WorldState.States.Hands;
using Content.Server.AI.HTN.WorldState.States.Inventory;
using Content.Server.GameObjects;
using Content.Shared.GameObjects.Components.Inventory;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Sequence.Clothing
{
    // TODO: Make Sequence
    public class PickupNearbyClothing : SequenceTask
    {
        private IEntity _nearestClothing;
        private EquipmentSlotDefines.Slots _slot;
        public PickupNearbyClothing(IEntity owner, EquipmentSlotDefines.Slots slot) : base(owner)
        {
            _slot = slot;
        }

        public override string Name => "PickupNearbyClothing";

        public override bool PreconditionsMet(AiWorldState context)
        {
            var nearbyClothing = context.GetState<NearbyClothing>();
            var freeHands = context.GetState<FreeHands>();

            if (freeHands.GetValue() == 0)
            {
                return false;
            }

            foreach (var entity in nearbyClothing.GetValue())
            {
                // If someone already has it / not clothing / wrong slot
                if (!entity.TryGetComponent(out ItemComponent itemComponent) || itemComponent.IsEquipped) continue;
                if (!entity.TryGetComponent(out ClothingComponent clothingComponent)) continue;
                if (clothingComponent.SlotFlags == EquipmentSlotDefines.SlotFlags.PREVENTEQUIP ||
                    (clothingComponent.SlotFlags & EquipmentSlotDefines.SlotMasks[_slot]) == 0)
                {
                    continue;
                }
                _nearestClothing = entity;
                return true;

            }

            return false;

        }

        public override void SetupSubTasks(AiWorldState context)
        {
            SubTasks = new IAiTask[]
            {
                new PickupItem(Owner, _nearestClothing),
                new MoveToEntity(Owner, _nearestClothing),
            };
        }
    }
}
