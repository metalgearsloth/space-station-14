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
    public sealed class PickupNearestDrink : SequenceTask
    {
        private IEntity _nearestDrink;
        public PickupNearestDrink(IEntity owner) : base(owner)
        {

        }

        public override string Name => "PickupNearestDrink";

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

            foreach (var entity in context.GetEnumerableStateValue<NearbyDrink, IEntity>())
            {
                // If someone already has it then skip
                if (!entity.TryGetComponent(out ItemComponent itemComponent) || itemComponent.IsEquipped) continue;
                _nearestDrink = entity;
                return true;
            }

            return false;

        }

        public override void SetupSubTasks(AiWorldState context)
        {
            SubTasks = new IAiTask[]
            {
                new PickupItem(Owner, _nearestDrink),
                new DropHandItems(Owner),
                new MoveToEntity(Owner, _nearestDrink),
            };
        }
    }
}
