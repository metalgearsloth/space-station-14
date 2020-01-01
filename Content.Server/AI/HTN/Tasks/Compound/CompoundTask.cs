using System.Collections.Generic;
using Content.Server.AI.HTN.WorldState;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Compound
{
    public abstract class CompoundTask : IAiTask
    {
        public abstract string Name { get; }

        protected IEntity Owner { get; }

        /// <summary>
        /// What order will methods be tried to find a valid one
        /// </summary>
        public virtual DecompositionRule DecompositionRule => DecompositionRule.Ordered;

        /// <summary>
        /// The available compound / primitive tasks that can be run to fulfil this compound task
        /// </summary>
        public abstract List<IAiTask> Methods { get; }

        public abstract bool PreconditionsMet(AiWorldState context);

        public CompoundTask(IEntity owner)
        {
            Owner = owner;
        }
    }

    public enum DecompositionRule
    {
        Random,
        Ordered,
    }
}
