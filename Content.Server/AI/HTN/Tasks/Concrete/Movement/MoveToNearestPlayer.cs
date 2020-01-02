using System.Collections.Generic;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Primitive.Movement
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
            var nearbyPlayers = context.GetState<NearbyPlayers>();
            foreach (var entity in nearbyPlayers.GetValue())
            {
                _nearestPlayer = entity;
                return true;
            }

            return false;
        }

        public override void SetupOperator()
        {
            TaskOperator = new Operators.Movement.MoveToEntity(Owner, _nearestPlayer);
        }
    }
}
