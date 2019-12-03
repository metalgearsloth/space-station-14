using Content.Server.AI.Routines.Movers;
using Robust.Server.AI;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.Routines
{
    /// <summary>
    ///  Will move towards the specified entity if necessary and wait near them until they get out of range
    /// </summary>
    public class FollowRoutine : AiRoutine
    {
        private MoveToEntityAiRoutine _mover = new MoveToEntityAiRoutine();
        public IEntity FollowTarget { get; set; }
        public float MaxDistance { get; set; } = 2.0f;

        public override void Setup(IEntity owner, AiLogicProcessor processor)
        {
            base.Setup(owner, Processor);
            _mover.Setup(owner, Processor);
        }

        public override void Update(float frameTime)
        {
            // TODO: Throttle if no route
            base.Update(frameTime);
            if (FollowTarget == null)
            {
                return;
            }

            if (!_mover.Arrived)
            {
                _mover.HandleMovement(frameTime);
                return;
            }

            if ((Owner.Transform.GridPosition.Position - Owner.Transform.GridPosition.Position).Length > MaxDistance)
            {
                _mover.TargetProximity = MaxDistance;
                _mover.GetRoute(FollowTarget);
            }
        }
    }
}
