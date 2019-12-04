using System.Collections.Generic;
using Content.Server.AI.Actions;

namespace Content.Server.AI.Goals
{
    public class SatisfyHungerGoal : IGoapGoal
    {
        public HashSet<GoapAction> Actions { get; } = new HashSet<GoapAction>()
        {
            new PickupFood(),
            new EatFoodInHandsAction(),
        };

        public HashSet<KeyValuePair<string, bool>> GoalState { get; } = new HashSet<KeyValuePair<string, bool>>
        {
            new KeyValuePair<string, bool>("Hungry", false)
        };
    }
}
