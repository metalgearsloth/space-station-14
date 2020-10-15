using System;
using Content.Server.GameObjects.Components.Mobs;
using Content.Shared.GameObjects.EntitySystemMessages;
using Content.Shared.GameObjects.EntitySystems;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Input.Binding;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.GameObjects.EntitySystems.Click
{
    /// <summary>
    /// Governs interactions during clicking on entities
    /// </summary>
    [UsedImplicitly]
    public sealed class InteractionSystem : SharedInteractionSystem
    {
        public override void Initialize()
        {
            base.Initialize();
            SubscribeNetworkEvent<DragDropMessage>(HandleDragDropMessage);
        }

        public override void Shutdown()
        {
            CommandBinds.Unregister<InteractionSystem>();
            base.Shutdown();
        }

        /// <summary>
        /// Entity will try and use their active hand at the target location.
        /// Don't use for players
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="coords"></param>
        /// <param name="uid"></param>
        internal void UseItemInHand(IEntity entity, EntityCoordinates coords, EntityUid uid)
        {
            if (entity.HasComponent<BasicActorComponent>())
            {
                throw new InvalidOperationException();
            }

            if (entity.TryGetComponent(out CombatModeComponent combatMode) && combatMode.IsInCombatMode)
            {
                DoAttack(entity, coords, false, uid);
            }
            else
            {
                UserInteraction(entity, coords, uid);
            }
        }
    }
}
