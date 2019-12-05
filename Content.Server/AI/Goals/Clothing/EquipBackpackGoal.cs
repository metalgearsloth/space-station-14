using System.Collections.Generic;
using Content.Server.AI.Actions;
using Content.Server.GameObjects;
using Content.Shared.GameObjects.Components.Inventory;

namespace Content.Server.AI.Goals.Clothing
{
    public class EquipBackpackGoal
    {
        public string Name => "EquipBackpack";
        public HashSet<GoapAction> Actions { get; } = new HashSet<GoapAction>()
        {
            // TODO: Need a pickupclothing action
            new PickupClothingAction(EquipmentSlotDefines.Slots.BACKPACK,
                null,
                new Dictionary<string, bool>{{"HasBackpack", true}}),

            new UseItemInHandsAction(typeof(ClothingComponent),
                new Dictionary<string, bool>{{"HasBackpack", true}},
                new Dictionary<string, bool>{{"EquippedBackpack", true}}),
        };

        public IDictionary<string, bool> GoalState { get; } = new Dictionary<string, bool>
        {
            {"EquippedBackpack", true}
        };
    }
}
