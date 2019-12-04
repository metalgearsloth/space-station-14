using System.Collections.Generic;
using Content.Server.AI.Actions;
using Content.Server.AI.Preconditions;

namespace Content.Server.AI.Goals
{
    public class GoapGoal
    {
        public HashSet<GoapAction> Actions { get; set; }
        public HashSet<KeyValuePair<string, bool>> GoalState { get; set; }
    }
}
