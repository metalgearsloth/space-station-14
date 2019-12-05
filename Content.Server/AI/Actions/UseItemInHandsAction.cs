using System;
using System.Collections.Generic;
using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Nutrition;
using Content.Server.GameObjects.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;

namespace Content.Server.AI.Actions
{
    /// <summary>
    /// A generic action to be re-used
    /// </summary>
    public class UseItemInHandsAction : GoapAction
    {
        private readonly Type _component;

        public override bool RequiresInRange { get; set; } = false;

        public override float Range { get; set; } = InteractionSystem.InteractionRange - 0.01f;
        // TODO: We need a range for search radius and a range for how close to get

        public UseItemInHandsAction(Type component, IDictionary<string, bool> preConditions = null, IDictionary<string, bool> effects = null)
        {
            if (component.IsAssignableFrom(typeof(Component)))
            {
                Logger.FatalS("ai", $"PickupComponent needs a valid Component");
                throw new InvalidOperationException();
            }
            _component = component;

            PreConditions.Add(new KeyValuePair<string, bool>("HasHands", true));

            if (preConditions != null)
            {
                foreach (var preCon in preConditions)
                {
                    PreConditions.Add(preCon);
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

            // Need to find the item in slots
            foreach (var item in handsComponent.GetAllHeldItems())
            {
                if (item.Owner.GetComponent(_component) == null)
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
