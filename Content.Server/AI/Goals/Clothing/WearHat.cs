using System.Collections.Generic;
using Content.Server.AI.Actions;
using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Nutrition;

namespace Content.Server.AI.Goals.Clothing
{
    public class WearHat : IGoapGoal
    {
        public HashSet<GoapAction> Actions { get; } = new HashSet<GoapAction>()
        {
            new PickupComponentAction(typeof(ClothingComponent),
                null,
                new Dictionary<string, bool>{{"HasHat", true}}),

            new UseItemInHandsAction(typeof(ClothingComponent),
                new Dictionary<string, bool>{{"HasHat", true}},
                new Dictionary<string, bool>{{"WearingHat", true}}),
        };

        public HashSet<KeyValuePair<string, bool>> GoalState { get; } = new HashSet<KeyValuePair<string, bool>>
        {
            new KeyValuePair<string, bool>("WearingHat", true)
        };
    }
}
