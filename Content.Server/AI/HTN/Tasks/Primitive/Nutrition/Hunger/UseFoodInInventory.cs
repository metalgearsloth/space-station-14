using System.Collections.Generic;
using Content.Server.AI.HTN.Tasks.Primitive.Operators;
using Content.Server.AI.HTN.Tasks.Primitive.Operators.Inventory;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States.Inventory;
using Content.Server.GameObjects.Components.Nutrition;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Primitive.Nutrition.Hunger
{
    public sealed class UseFoodInInventory : PrimitiveTask
    {
        public override string Name => "UseFoodInInventory";

        private IEntity _targetFood;
        public override PrimitiveTaskType TaskType => PrimitiveTaskType.Interaction;

        public UseFoodInInventory(IEntity owner) : base(owner) {}

        public override bool PreconditionsMet(AiWorldState context)
        {
            foreach (var item in context.GetStateValue<InventoryState, List<IEntity>>())
            {
                if (item.HasComponent<FoodComponent>())
                {
                    _targetFood = item;
                    return true;
                }
            }

            return false;
        }

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
