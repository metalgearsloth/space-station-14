using Content.Server.AI.HTN.Tasks.Concrete.Operators.Movement;
using Content.Server.AI.HTN.Tasks.Primitive;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Concrete.Movement
{
    public sealed class MoveToNearestPlayer : ConcreteTask
    {
        private IEntity _nearestPlayer;

        public MoveToNearestPlayer(IEntity owner) : base(owner)
        {

        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            // TODO: CanMove
            foreach (var entity in context.GetEnumerableStateValue<NearbyPlayers, IEntity>())
            {
                _nearestPlayer = entity;
                return true;
            }

            return false;
        }

        public override void SetupOperator()
        {
            TaskOperator = new MoveToEntityOperator(Owner, _nearestPlayer);
        }
    }
}
