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
    public class EatFoodAction : GoapAction
    {
        public override bool RequiresInRange { get; set; } = false;
        private IEntity _targetEntity;

        public EatFoodAction()
        {
            PreConditions.Add(new KeyValuePair<AiState, bool>(AiState.Hungry, true));
            Effects.Add(new KeyValuePair<AiState, bool>(AiState.Hungry, false));
        }

        public override void Reset()
        {
            base.Reset();
            _targetEntity = null;
        }

        public override float Cost()
        {
            return 1.0f;
        }

        public override bool CheckProceduralPreconditions(IEntity entity)
        {
            if (!entity.TryGetComponent(out HandsComponent handsComponent))
            {
                return false;
            }

            foreach (var item in handsComponent.GetAllHeldItems())
            {
                if (item.Owner.HasComponent<FoodComponent>())
                {
                    _targetEntity = item.Owner;
                    return true;
                }
            }

            return false;
        }

        public override bool TryPerformAction(IEntity entity)
        {
            var entitySystemManager = IoCManager.Resolve<IEntitySystemManager>();
            var interactionSystem = entitySystemManager.GetEntitySystem<InteractionSystem>();

            if (!entity.TryGetComponent(out HandsComponent handsComponent))
            {
                return false;
            }

            if (handsComponent.GetActiveHand?.Owner == _targetEntity)
            {
                interactionSystem.UseItemInHand(entity);
            }

            return false;
        }
    }
}
