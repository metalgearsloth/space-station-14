using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.Routines
{
    /// <summary>
    /// AiRoutines are supposed to be small generic behaviors re-used between Ai logic processors.
    /// E.g. routine to move to a specific location, or to melee attack a target, etc.
    /// </summary>
    public abstract class AiRoutine
    {
        public virtual void Setup(IEntity owner) {}
        public virtual void Update() {}
        public virtual bool RequiresMover => false;
        public virtual void InjectMover(MovementAiRoutine mover) {}
    }
}
