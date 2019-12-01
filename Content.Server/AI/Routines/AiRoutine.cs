using System;
using Content.Server.AI.Routines.Movers;
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
        // How long between the routine between the routine needing to run logic. Something that moves long distances will have a higher cooldown whereas something fighting will have a lower cooldown
        protected DateTime LastProcess = DateTime.Now;
        protected virtual float ProcessCooldown { get; set; }
        protected virtual IEntity Owner { get; set; }
        public AiLogicProcessor Processor { get; set; }

        public virtual void Setup(IEntity owner)
        {
            Owner = owner;
        }
        public virtual void Update() {}
        public virtual bool RequiresMover => false;
        public virtual void InjectMover(MoveToEntityAiRoutine mover) {}
    }
}
