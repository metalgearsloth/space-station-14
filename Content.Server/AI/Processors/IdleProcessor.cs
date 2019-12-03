using System;
using Content.Server.AI.Routines;
using JetBrains.Annotations;
using Robust.Server.AI;
using Robust.Shared.Interfaces.Random;
using Robust.Shared.IoC;
using Robust.Shared.Maths;

namespace Content.Server.AI.Processors
{
    /// <summary>
    /// Will just hang around random spots
    /// </summary>
    [UsedImplicitly]
    [AiLogicProcessor("Idle")]
    public class IdleProcessor : BaseAiProcessor
    {
        // Routines
        private readonly IdleAtRoutine _idle = new IdleAtRoutine();

        public override void Setup()
        {
            base.Setup();

            // Routine setup
            _idle.Setup(SelfEntity, this);

            // Finalise
            ChangeRoutine(_idle);
        }

        protected override void ProcessLogic(float frameTime)
        {
            base.ProcessLogic(frameTime);
            // Pick a random spot in range and idle around there
            var robustRandom = IoCManager.Resolve<IRobustRandom>();
            var angle = Angle.FromDegrees(robustRandom.Next(359));
            var distance = robustRandom.Next(5);
            var idleVector = SelfEntity.Transform.GridPosition.Position + angle.ToVec() * distance;
            var targetSpot = SelfEntity.Transform.GridPosition.Offset(idleVector);
            _idle.IdleSpot = targetSpot;

            // Set random duration until the next update
            ProcessCooldown = robustRandom.Next(15, 45);
        }
    }
}
