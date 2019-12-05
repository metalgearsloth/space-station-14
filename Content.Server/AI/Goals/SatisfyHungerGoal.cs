using System.Collections.Generic;
using Content.Server.AI.Actions;
using Content.Server.GameObjects.Components.Nutrition;

namespace Content.Server.AI.Goals
{
    public class SatisfyHungerGoal : IGoapGoal
    {
        public string Name => "SatisfyHunger";
        public HashSet<GoapAction> Actions { get; } = new HashSet<GoapAction>()
        {
            new PickupComponentAction(typeof(FoodComponent),
                null,
                new Dictionary<string, bool>{{"HasFood", true}}),

            new UseItemInHandsAction(typeof(FoodComponent),
                new Dictionary<string, bool>{{"HasFood", true}},
            new Dictionary<string, bool>{{"Hungry", false}}),
        };

        public HashSet<KeyValuePair<string, bool>> GoalState { get; } = new HashSet<KeyValuePair<string, bool>>
        {
            new KeyValuePair<string, bool>("Hungry", false)
        };
    }
}
