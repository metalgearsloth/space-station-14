using System.Collections.Generic;
using Content.Server.AI.Actions;
using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Nutrition;
using Content.Shared.GameObjects.Components.Inventory;

namespace Content.Server.AI.Goals.Clothing
{
    public class EquipHeadGoal : IGoapGoal
    {
        public string Name => "EquipHat";
        public HashSet<GoapAction> Actions { get; } = new HashSet<GoapAction>()
        {
            new PickupClothingAction(EquipmentSlotDefines.Slots.HEAD,
                null,
                new Dictionary<string, bool>{{"HasHead", true}}),

            new UseItemInHandsAction(typeof(ClothingComponent),
                new Dictionary<string, bool>{{"HasHead", true}},
                new Dictionary<string, bool>{{"EquippedHead", true}}),
        };

        public IDictionary<string, bool> GoalState { get; } = new Dictionary<string, bool>
        {
            {"EquippedHead", true}
        };
    }
}
