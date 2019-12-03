using System;
using Robust.Server.AI;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.Routines
{
    /// <summary>
    /// AiRoutines are supposed to be small generic behaviors re-used between Ai logic processors.
    /// E.g. routine to move to a specific location, or to melee attack a target, etc.
    /// </summary>
    public abstract class AiRoutine
    {
        /// <summary>
        /// If there is a cooldown between the routine needing to run logic (shouldn't apply to movement)
        /// </summary>
        protected virtual float ProcessCooldown { get; set; }
        protected float RemainingProcessCooldown = 0.0f;
        protected IEntity Owner { get; set; }
        protected AiLogicProcessor Processor { get; set; }

        // TODO: Look at just making this a constructor
        /// <summary>
        /// Called once at processor startup.
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="processor"></param>
        public virtual void Setup(IEntity owner, AiLogicProcessor processor)
        {
            Owner = owner;
            Processor = processor;
        }

        /// <summary>
        /// Called every frame by the processor if this routine is active.
        /// Any calls to base should be under the routine specific updates.
        /// </summary>
        /// <param name="frameTime"></param>
        public virtual void Update(float frameTime)
        {
            if (RemainingProcessCooldown <= 0)
            {
                RemainingProcessCooldown = ProcessCooldown;
                return;
            }
            RemainingProcessCooldown -= frameTime;
        }

        /// <summary>
        /// Gets called when the routine becomes active by a processor
        /// </summary>
        public virtual void ActiveRoutine() {}

        /// <summary>
        /// Gets called when the routine is no longer the active one
        /// </summary>
        public virtual void InactiveRoutine() {}
    }
}
