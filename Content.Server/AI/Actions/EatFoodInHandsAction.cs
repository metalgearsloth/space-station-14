using System.Collections.Generic;
using Content.Server.AI.Preconditions;
using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Nutrition;
using Content.Server.GameObjects.EntitySystems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server.AI.Actions
{
    /// <summary>
    /// Eats equipped / in inventory food
    /// </summary>
    public class EatFoodInHandsAction : GoapAction
    {
        public override bool RequiresInRange { get; set; } = false;

        public EatFoodInHandsAction()
        {
            PreConditions.Add(new KeyValuePair<string, bool>("Hungry", false));
            PreConditions.Add(new KeyValuePair<string, bool>("HasFood", true));

            Effects.Add(new KeyValuePair<string, bool>("Hungry", false));
        }

        public override void Reset()
        {
            base.Reset();
        }

        public override float Cost()
        {
            return 1.0f;
        }

        public override bool CheckProceduralPreconditions(IEntity entity)
        {
            // TODO: Add ActionBlocker
            // Default WorldState for HasFood should be false so don't need to check if we have it
            if (!entity.TryGetComponent(out HandsComponent handsComponent))
            {
                return false;
            }

            return true;
        }

        public override bool TryPerformAction(IEntity entity)
        {
            var entitySystemManager = IoCManager.Resolve<IEntitySystemManager>();
            var interactionSystem = entitySystemManager.GetEntitySystem<InteractionSystem>();

            if (!entity.TryGetComponent(out HandsComponent handsComponent))
            {
                return false;
            }

            // Need to find the food
            foreach (var item in handsComponent.GetAllHeldItems())
            {
                if (!item.Owner.HasComponent<FoodComponent>())
                {
                    continue;
                }
                interactionSystem.TryUseInteraction(entity, TargetEntity);
                return true;
            }

            return false;
        }
    }
}
