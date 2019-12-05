using System.Collections.Generic;
using Content.Server.AI.Actions;

namespace Content.Server.AI.Goals.Clothing
{
    public class WearClothesGoal : IGoapGoal
    {
        public string Name => "WearClothes";

        public HashSet<GoapAction> Actions { get;} = new HashSet<GoapAction>()
        {

        };
        public IDictionary<string, bool> GoalState { get; } = new Dictionary<string, bool>
        {
            {"EquippedHat", true}
        };
    }
}
