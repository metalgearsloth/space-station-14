using System.Collections.Generic;
using Content.Server.AI.Preconditions;
using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Nutrition;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.Actions
{
    /// <summary>
    /// Eats equipped / in inventory food
    /// </summary>
    public class EatFood : GoapAction
    {
        public override float Cost { get; set; } = 1.0f;
        public override bool RequiresInRange { get; set; } = false;

        public EatFood()
        {
            PreConditions.Add(new KeyValuePair<AiState, bool>(AiState.Hungry, true));
            Effects.Add(new KeyValuePair<AiState, bool>(AiState.Hungry, false));
        }

        public override bool CheckProceduralPreconditions(GoapAgent agent)
        {
            if (!agent.Owner.TryGetComponent(out HandsComponent handsComponent))
            {
                return false;
            }

            foreach (var item in handsComponent.GetAllHeldItems())
            {
                if (item.Owner.HasComponent<FoodComponent>())
                {
                    Target = item.Owner;
                    return true;
                }
            }

            return false;
        }

        public override bool TryPerformAction(GoapAgent agent)
        {
            if (Target == null)
            {
                return false;
            }
            // TODO
            Target.TryGetComponent(out FoodComponent foodComponent);
            return false;
        }
    }
}
