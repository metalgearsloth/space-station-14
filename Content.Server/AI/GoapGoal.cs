using System.Collections.Generic;
using Content.Server.AI.Actions;

namespace Content.Server.AI
{
    public class GoapGoal
    {
        public HashSet<GoapAction> Actions { get; set; }
        public HashSet<KeyValuePair<GoapAction, bool>> GoalState { get; set; }
    }
}
