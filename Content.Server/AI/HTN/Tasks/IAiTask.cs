using System.Collections.Generic;
using Content.Server.AI.HTN.Tasks.Compound;
using Content.Server.AI.HTN.WorldState;

namespace Content.Server.AI.HTN.Tasks
{
    public interface IAiTask
    {
        string Name { get; }
        bool PreconditionsMet(AiWorldState context);
    }
}
