using System.Collections.Generic;
using Content.Server.AI.HTN.Tasks.Primitive.Operators;
using Content.Server.AI.HTN.Tasks.Primitive.Operators.Inventory;
using Content.Server.AI.HTN.WorldState;
using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Nutrition;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Primitive.Nutrition.Hunger
{
    public sealed class UseFoodInInventory : PrimitiveTask
    {
        private IEntity _targetFood;
        public override PrimitiveTaskType TaskType => PrimitiveTaskType.Interaction;

        public UseFoodInInventory(IEntity owner) : base(owner)
        {

        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            // TODO: Check hands then check inventory
            if (!Owner.TryGetComponent(out HandsComponent handsComponent))
            {
                return false;
            }

            // Find any food in hands
            foreach (var hand in handsComponent.GetHandIndices())
            {
                var item = handsComponent.GetHand(hand);
                if (item == null) continue;

                if (!item.Owner.HasComponent<FoodComponent>()) continue;

                _targetFood = item.Owner;
                return true;
            }
            return false;
        }

        public override HashSet<IStateData> ProceduralEffects => new HashSet<IStateData>
        {
            // new HungryState(false)
        };

        public override void SetupOperator()
        {
            TaskOperator = new UseItemOnPerson(Owner, _targetFood);
        }

        public override Outcome Execute(float frameTime)
        {

            var outcome = base.Execute(frameTime);
            if (outcome == Outcome.Failed)
            {
                return outcome;
            }

            // TODO: Keep eating it until stomach full
            return !_targetFood.Deleted ? Outcome.Continuing : Outcome.Success;
        }
    }
}
