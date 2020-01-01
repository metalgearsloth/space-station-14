using System.Collections.Generic;
using Content.Server.AI.HTN.Tasks.Primitive.Operators;
using Content.Server.AI.HTN.WorldState;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Primitive
{
    // 3 ways planner gets new plan: Current finishes / fails, NPC doesnt have one, or world state changes
    public abstract class PrimitiveTask : IAiTask
    {
        public abstract bool PreconditionsMet(AiWorldState context);
        public virtual IEntity Owner { get; }

        public PrimitiveTask() {}

        public PrimitiveTask(IEntity owner)
        {
            Owner = owner;
        }

        /// <summary>
        /// What should actually run the task. Primitive tasks are somewhat wrappers around operators
        /// </summary>
        protected IOperator TaskOperator { get; set; }

        public List<IAiTask> Methods => new List<IAiTask> {this};

        public virtual HashSet<IStateData> ProceduralEffects { get; } = new HashSet<IStateData>();

        public abstract void SetupOperator();

        // Call the task's operate with Execute and get the outcome
        public virtual Outcome Execute(float frameTime)
        {
            return TaskOperator.Execute(frameTime);
        }
    }

}
