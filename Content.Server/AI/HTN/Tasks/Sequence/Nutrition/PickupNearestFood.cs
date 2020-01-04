using Content.Server.AI.HTN.Tasks.Concrete.Inventory;
using Content.Server.AI.HTN.Tasks.Concrete.Movement;
using Content.Server.AI.HTN.Tasks.Primitive.Movement;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States.Hands;
using Content.Server.AI.HTN.WorldState.States.Inventory;
using Content.Server.AI.HTN.WorldState.States.Nutrition;
using Content.Server.GameObjects;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Sequence.Nutrition
{
    public sealed class PickupNearestFood : SequenceTask
    {
        private IEntity _nearestFood;
        public PickupNearestFood(IEntity owner) : base(owner)
        {

        }

        public override string Name => "PickupNearestFood";

        public override bool PreconditionsMet(AiWorldState context)
        {
            bool freeHand = false;

            foreach (var hand in context.GetEnumerableStateValue<FreeHands, string>())
            {
                freeHand = true;
                break;
            }

            if (!freeHand)
            {
                return false;
            }

            foreach (var entity in context.GetEnumerableStateValue<NearbyFood, IEntity>())
            {
                // If someone already has it then skip
                if (!entity.TryGetComponent(out ItemComponent itemComponent) || itemComponent.IsEquipped) continue;
                _nearestFood = entity;
                return true;
            }

            return false;

        }

        public override void SetupSubTasks(AiWorldState context)
        {
            SubTasks = new IAiTask[]
            {
                new PickupItem(Owner, _nearestFood),
                new DropHandItems(Owner),
                new MoveToEntity(Owner, _nearestFood),
            };
        }
    }
}
