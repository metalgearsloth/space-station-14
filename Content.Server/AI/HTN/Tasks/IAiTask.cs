using System.Collections.Generic;
using Content.Server.AI.HTN.WorldState;

namespace Content.Server.AI.HTN.Tasks
{
    public interface IAiTask
    {
        bool PreconditionsMet(AiWorldState context);

        /// <summary>
        /// If there are further compound tasks in this task we will recursively find them
        /// </summary>
        List<IAiTask> Methods { get; }
    }
}
