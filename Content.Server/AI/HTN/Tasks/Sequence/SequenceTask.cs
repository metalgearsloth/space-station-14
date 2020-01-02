using System.Collections.Generic;
using Content.Server.AI.HTN.WorldState;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Sequence
{
    /// <summary>
    ///  An abstract task with a list of subtasks
    /// If you want the behavior to branch to a specific task then use SelectorTask
    /// </summary>
    public abstract class SequenceTask : IAiTask
    {
        protected IEntity Owner { get; }
        public SequenceTask(IEntity owner)
        {
            Owner = owner;
        }

        public abstract string Name { get; }

        public virtual bool PreconditionsMet(AiWorldState context)
        {
            return true;
        }

        /// <summary>
        /// This should instantiate the SubTasks for this sequence, remembering that they should be in reverse-order
        /// </summary>
        /// <param name="context"></param>
        public abstract void SetupSubTasks(AiWorldState context);

        /// <summary>
        /// These should be in reverse order
        /// </summary>
        public IEnumerable<IAiTask> SubTasks { get; protected set; }
    }
}
