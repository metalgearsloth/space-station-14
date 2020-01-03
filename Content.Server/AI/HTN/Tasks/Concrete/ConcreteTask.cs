using System.Collections.Generic;
using Content.Server.AI.HTN.Tasks.Primitive.Operators;
using Content.Server.AI.HTN.WorldState;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Primitive
{
    // 3 ways planner gets new plan: Current finishes / fails, NPC doesnt have one, or world state changes
    /// <summary>
    /// Essentially a wrapper around an Operator which makes them reusable.
    /// e.g. PickupNearestMeleeWeapon, PickupNearestGun, PickupNearestLaser could all use the same operator.
    /// All Plans get decomposed into a series of Primitive tasks.
    /// </summary>
    public abstract class ConcreteTask : IAiTask
    {
        public string Name { get; }

        /// <summary>
        /// Checks whether the primitive task can be run. Also sets up variables needed
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public abstract bool PreconditionsMet(AiWorldState context);

        public virtual PrimitiveTaskType TaskType { get; } = PrimitiveTaskType.Default;

        protected IEntity Owner { get; }

        protected ConcreteTask(IEntity owner)
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

        // Call the task's operator with Execute and get the outcome
        public virtual Outcome Execute(float frameTime)
        {
            return TaskOperator.Execute(frameTime);
        }
    }

    public enum PrimitiveTaskType
    {
        Default, // Default
        Interaction,
        MeleeAttack,
        RangedAttack,
    }
}
