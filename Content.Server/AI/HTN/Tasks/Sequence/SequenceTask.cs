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
        public abstract string Name { get; }
        protected IEntity Owner { get; }

        /// <summary>
        /// These should be in reverse order and should be set by SetupSubTasks.
        /// This is so the precondition can cache any values needed
        /// </summary>
        public IEnumerable<IAiTask> SubTasks { get; protected set; }

        public SequenceTask(IEntity owner)
        {
            Owner = owner;
        }

        public virtual bool PreconditionsMet(AiWorldState context)
        {
            return true;
        }

        /// <summary>
        /// This should instantiate the SubTasks for this sequence in REVERSE ORDER.
        /// </summary>
        /// <param name="context"></param>
        public abstract void SetupSubTasks(AiWorldState context);
    }
}
