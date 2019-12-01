using System;
using System.Collections.Generic;
using Content.Server.AI.Routines;
using Robust.Server.AI;

namespace Content.Server.AI
{
    public abstract class BaseAiProcessor : AiLogicProcessor
    {
        // TODO: Potentially change RobustServer
        public AiRoutine ActiveRoutine => _activeRoutine;
        private AiRoutine _activeRoutine;

        public abstract IEnumerable<AiRoutine> GetRoutines();

        public override void Setup()
        {
            base.Setup();
            foreach (var routine in GetRoutines())
            {
                routine.Setup(SelfEntity);
                routine.Processor = this;
            }
        }

        protected void ChangeActiveRoutine(AiRoutine routine)
        {
            _activeRoutine = routine;
        }

        protected virtual void ProcessLogic() {}

        public override void Update(float frameTime)
        {
            ProcessLogic();
            ActiveRoutine.Update();
        }
    }
}
