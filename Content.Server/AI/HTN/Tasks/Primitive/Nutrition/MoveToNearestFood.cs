using Content.Server.AI.HTN.Tasks.Primitive.Operators.Movement;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Primitive.Nutrition
{
    public sealed class MoveToNearestFood : PrimitiveTask
    {
        private IEntity _nearestFood;
        public MoveToNearestFood(IEntity owner) : base(owner)
        {

        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            var nearbyFood = context.GetState<NearbyFood>();

            foreach (var entity in nearbyFood.GetValue())
            {
                _nearestFood = entity;
            }

            return _nearestFood != null;

        }

        public override void SetupOperator()
        {
            TaskOperator = new MoveToEntity(Owner, _nearestFood);
        }
    }
}
