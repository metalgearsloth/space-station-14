using System.Collections.Generic;
using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Nutrition;
using Content.Server.GameObjects.EntitySystems;
using Robust.Server.AI;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.Actions
{
    public class PickupFood : GoapAction
    {
        private List<IEntity> _nearbyFood = new List<IEntity>();
        public override float Cost { get; set; } = 5.0f;

        public override float Range { get; set; } = InteractionSystem.InteractionRange - 0.01f;
        // TODO: We need a range for search radius and a range for how close to get

        public override void Reset()
        {
            base.Reset();
            _nearbyFood.Clear();
        }

        // TODO: Use entity ailogic vision
        public override bool CheckProceduralPreconditions(IEntity entity)
        {
            if (!entity.TryGetComponent(out AiLogicProcessor processor))
            {
                return false;
            }

            foreach (var _ in Utils.GetComponentOwnersInRange(entity.Transform.GridPosition, typeof(FoodComponent),
                processor.VisionRadius))
            {
                // TODO: Setup what we're tracking here?
                return true;
            }

            return false;
        }

        public override bool InRange(IEntity entity)
        {
            if (Target == null)
            {
                return false;
            }

            return (Target.Transform.GridPosition.Position - entity.Transform.GridPosition.Position).Length <= Range;
        }

        public override bool TryPerformAction(IEntity entity)
        {
            // TODO: Interact with it instead of putting it straight in
            if ((entity.Transform.GridPosition.Position - Target.Transform.GridPosition.Position).Length <=
                InteractionSystem.InteractionRange)
            {
                entity.TryGetComponent(out HandsComponent handsComponent);
                // TODO: Interact with item instead
                Target.TryGetComponent(out ItemComponent itemComponent);
                handsComponent.PutInHand(itemComponent);
                if (handsComponent.GetActiveHand != itemComponent)
                {
                    handsComponent.SwapHands();
                }
                return true;
            }

            return false;
        }
    }
}
