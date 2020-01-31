using System.Collections.Generic;
using Content.Server.AI.HTN.WorldState;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Compound
{
    /// <summary>
    /// Contains a list of methods (compound task or primitive task) available to achieve this compound task
    /// The first achieveable entry (where the preconditions are met) will be used for the plan
    /// Compound tasks get decomposed until eventually all that remains are primitive tasks
    /// </summary>
    public abstract class SelectorTask : IAiTask
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
        public List<IAiTask> Methods { get; protected set; } = new List<IAiTask>();

        /// <summary>
        /// Setup all method variables required and also instantiate the methods
        /// </summary>
        public abstract void SetupMethods(AiWorldState context);

        /// <summary>
        ///  Apply the outcome of this task to the worldstate e.g. 1 less free hand, no longer hungry, etc.
        /// For planning purposes only so it doesn't need to actually reflect reality.
        /// </summary>
        /// <param name="context"></param>
        public virtual void ProceduralEffects(in AiWorldState context) {}

        /// <summary>
        /// Checks whether the compound task can be run. Also sets up variables needed
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public abstract bool PreconditionsMet(AiWorldState context);

        public SelectorTask(IEntity owner)
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
