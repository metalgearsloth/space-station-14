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
        public HashSet<KeyValuePair<string, bool>> GoalState { get;}
    }
}
