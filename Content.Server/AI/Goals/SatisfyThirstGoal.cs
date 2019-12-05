using System.Collections.Generic;
using Content.Server.AI.Actions;
using Content.Server.GameObjects.Components.Nutrition;

namespace Content.Server.AI.Goals
{
    public class SatisfyThirstGoal : IGoapGoal
    {
        public string Name => "SatisfyThirst";

        public HashSet<GoapAction> Actions { get; } = new HashSet<GoapAction>()
        {
            new PickupComponentAction(typeof(DrinkComponent),
                null,
                new Dictionary<string, bool> {{"HasDrink", true}}),

            new UseItemInHandsAction(typeof(DrinkComponent),
                new Dictionary<string, bool> {{"HasDrink", true}},
                new Dictionary<string, bool> {{"Thirsty", false}}),
        };

        public IDictionary<string, bool> GoalState { get; } = new Dictionary<string, bool>
        {
            {"Thirsty", false}
        };
    }
}
