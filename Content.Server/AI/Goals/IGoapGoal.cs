using System.Collections.Generic;
using Content.Server.AI.Actions;
using Content.Server.AI.Preconditions;

namespace Content.Server.AI.Goals
{
    public interface IGoapGoal
    {
        HashSet<GoapAction> Actions { get;}
        HashSet<KeyValuePair<string, bool>> GoalState { get;}
    }
}
