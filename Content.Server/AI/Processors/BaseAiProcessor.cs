using System.Collections.Generic;
using Content.Server.AI.Routines;
using Robust.Server.AI;
using Robust.Server.Interfaces.Timing;
using Robust.Shared.IoC;

namespace Content.Server.AI.Processors
{
    public abstract class BaseAiProcessor : AiLogicProcessor
    {
        // TODO: Potentially change RobustServer
        public AiRoutine ActiveRoutine => _activeRoutine;
        private AiRoutine _activeRoutine;

        /// <summary>
        /// How long we should wait before running high-level logic. Routines are updated independently of this
        /// </summary>
        public virtual float ProcessCooldown { get; set; } = 0.0f;
        private float _cooldownRemaining = 0.0f;

        protected AiRoutine IdleRoutine = new IdleRoutine();

        public override void Setup()
        {
            base.Setup();
            ChangeRoutine(IdleRoutine);
        }

        protected virtual void ProcessLogic(float frameTime) {}

        /// <summary>
        /// Changes the routine that should be used
        /// </summary>
        /// <param name="routine"></param>
        protected virtual void ChangeRoutine(AiRoutine routine)
        {
            if (routine == _activeRoutine)
            {
                return;
            }
            _activeRoutine?.InactiveRoutine();
            _activeRoutine = routine;
            _activeRoutine.ActiveRoutine();
        }

        /// <summary>
        /// Updates the processor. Most of the time you won't need to update this.
        /// </summary>
        /// <param name="frameTime"></param>
        public override void Update(float frameTime)
        {
            _cooldownRemaining -= frameTime;

            if (_cooldownRemaining <= 0)
            {
                ProcessLogic(frameTime);
                _cooldownRemaining = ProcessCooldown;
            }

            ActiveRoutine.Update(frameTime);
        }
    }
}
