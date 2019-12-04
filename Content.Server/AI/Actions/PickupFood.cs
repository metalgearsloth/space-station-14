using System.Collections.Generic;
using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Nutrition;
using Content.Server.GameObjects.EntitySystems;
using Robust.Server.AI;
using Robust.Server.Interfaces.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server.AI.Actions
{
    public class PickupFood : GoapAction
    {
        private IEntity _targetEntity;

        public override float Cost()
        {
            return 5.0f;
        }

        public override float Range { get; set; } = InteractionSystem.InteractionRange - 0.01f;
        // TODO: We need a range for search radius and a range for how close to get

        public override void Reset()
        {
            base.Reset();
            _targetEntity = null;
        }

        // TODO: Use entity ailogic vision
        public override bool CheckProceduralPreconditions(IEntity entity)
        {
            if (!entity.TryGetComponent(out AiLogicProcessor processor))
            {
                return false;
            }

            foreach (var food in Utils.Visbility.GetComponentOwnersInRange(entity.Transform.GridPosition, typeof(FoodComponent),
                processor.VisionRadius))
            {
                _targetEntity = food;
                // TODO: Setup what we're tracking here?
                return true;
            }

            return false;
        }

        public override bool TryPerformAction(IEntity entity)
        {
            // If out of range for w/e reason
            if (!((entity.Transform.GridPosition.Position - _targetEntity.Transform.GridPosition.Position).Length <
                  InteractionSystem.InteractionRange))
            {
                return false;
            }

            if (!entity.TryGetComponent(out HandsComponent handsComponent))
            {
                return false;
            }

            var entitySystemManager = IoCManager.Resolve<IEntitySystemManager>();
            if (!entitySystemManager.TryGetEntitySystem(out InteractionSystem interactionSystem))
            {
                return false;
            }

            interactionSystem.Interaction(entity, _targetEntity);

            return true;

        }
    }
}
