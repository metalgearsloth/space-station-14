using System;
using System.Collections.Generic;
using Content.Server.AI.Processors;
using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Movement;
using Content.Server.GameObjects.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;

namespace Content.Server.AI.Actions
{
    /// <summary>
    /// A generic action specifying the designated component should be picked up
    /// </summary>
    public class PickupComponentAction : GoapAction
    {
        protected Type Component;
        public override bool RequiresInRange { get; set; } = true;

        public override float Range { get; set; } = InteractionSystem.InteractionRange - 0.01f;
        // TODO: We need a range for search radius and a range for how close to get

        public PickupComponentAction(Type component, IDictionary<string, bool> preConditions = null, IDictionary<string, bool> effects = null)
        {
            if (component.IsAssignableFrom(typeof(Component)))
            {
                Logger.FatalS("ai", $"PickupComponent needs a valid Component");
                throw new InvalidOperationException();
            }
            Component = component;

            PreConditions.Add(new KeyValuePair<string, bool>("HasHands", true));

            if (preConditions != null)
            {
                foreach (var precon in preConditions)
                {
                    Effects.Add(precon);
                }
            }

            if (effects != null)
            {
                foreach (var effect in effects)
                {
                    Effects.Add(effect);
                }
            }
        }

        public override bool InRange(IEntity entity)
        {
            if (TargetEntity == null)
            {
                return false;
            }

            return (entity.Transform.GridPosition.Position - TargetEntity.Transform.GridPosition.Position).Length <
                   InteractionSystem.InteractionRange - 0.01f;
        }

        public override float Cost()
        {
            return 5.0f;
        }

        // TODO: Use entity ailogic vision
        public override bool CheckProceduralPreconditions(IEntity entity)
        {
            if (!entity.TryGetComponent(out GoapProcessor processor))
            {
                return false;
            }

            // TODO: Add AiLogicProcessor VisionRange
            // TODO: PickNearest
            foreach (var comp in AIUtils.Visibility.GetEntitiesInRange(entity.Transform.GridPosition, Component,
                processor.VisionRadius))
            {
                TargetEntity = comp;
                return true;
            }

            return false;
        }

        public override bool TryPerformAction(IEntity entity)
        {
            if (TargetEntity == null)
            {
                return false;
            }

            // If out of range for w/e reason
            if ((entity.Transform.GridPosition.Position - TargetEntity.Transform.GridPosition.Position).Length >=
                  InteractionSystem.InteractionRange)
            {
                return false;
            }

            if (!entity.TryGetComponent(out HandsComponent handsComponent) ||
                !TargetEntity.TryGetComponent(out ItemComponent itemComponent))
            {
                return false;
            }

            // Someone else got it
            if (itemComponent.IsEquipped && itemComponent.Holder != entity)
            {
                return false;
            }

            // Need to get a free hand
            if (!handsComponent.CanPutInHand(itemComponent))
            {
                return false;
            }

            // TODO: If someone got 3 hands?
            if (handsComponent.GetActiveHand != null)
            {
                handsComponent.SwapHands();
            }

            var entitySystemManager = IoCManager.Resolve<IEntitySystemManager>();
            if (!entitySystemManager.TryGetEntitySystem(out InteractionSystem interactionSystem))
            {
                return false;
            }

            interactionSystem.Interaction(entity, TargetEntity);

            return true;
        }
    }
}
