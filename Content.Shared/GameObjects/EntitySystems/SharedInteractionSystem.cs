#nullable enable
using System.Linq;
using System.Threading.Tasks;
using Content.Shared.GameObjects.Components.Inventory;
using Content.Shared.GameObjects.Components.Items;
using Content.Shared.GameObjects.Components.Mobs;
using Content.Shared.GameObjects.EntitySystemMessages;
using Content.Shared.Input;
using Content.Shared.Interfaces;
using Content.Shared.Interfaces.GameObjects.Components;
using Content.Shared.Physics;
using Content.Shared.Utility;
using JetBrains.Annotations;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.GameObjects.Components;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.Interfaces.Physics;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Players;

namespace Content.Shared.GameObjects.EntitySystems
{
    /// <summary>
    /// Governs interactions during clicking on entities
    /// </summary>
    [UsedImplicitly]
    public abstract class SharedInteractionSystem : EntitySystem
    {
        [Dependency] private readonly IMapManager _mapManager = default!;
        [Dependency] private readonly IPhysicsManager _physicsManager = default!;

        public const float InteractionRange = 2;
        public const float InteractionRangeSquared = InteractionRange * InteractionRange;

        public delegate bool Ignored(IEntity entity);

        public override void Initialize()
        {
            base.Initialize();
            CommandBinds.Builder
                .Bind(EngineKeyFunctions.Use,
                    new PointerInputCmdHandler(HandleClientUseItemInHand))
                .Bind(ContentKeyFunctions.WideAttack,
                    new PointerInputCmdHandler(HandleWideAttack))
                .Bind(ContentKeyFunctions.ActivateItemInWorld,
                    new PointerInputCmdHandler(HandleActivateItemInWorld))

                .Register<SharedInteractionSystem>();
        }

        private bool HandleActivateItemInWorld(ICommonSession? session, EntityCoordinates coords,
            EntityUid uid)
        {
            if (!EntityManager.TryGetEntity(uid, out var used))
                return false;

            var playerEnt = session?.AttachedEntity;

            if (playerEnt == null || !playerEnt.IsValid())
            {
                return false;
            }

            if (!playerEnt.Transform.Coordinates.InRange(EntityManager, used.Transform.Coordinates, InteractionRange))
            {
                return false;
            }

            InteractionActivate(playerEnt, used);
            return true;
        }

        /// <summary>
        /// Activates the Activate behavior of an object
        /// Verifies that the user is capable of doing the use interaction first
        /// </summary>
        /// <param name="user"></param>
        /// <param name="used"></param>
        public void TryInteractionActivate(IEntity? user, IEntity? used)
        {
            if (user != null && used != null && ActionBlockerSystem.CanUse(user))
            {
                InteractionActivate(user, used);
            }
        }

        private void InteractionActivate(IEntity user, IEntity used)
        {
            var activateMsg = new ActivateInWorldMessage(user, used);
            RaiseLocalEvent(activateMsg);
            if (activateMsg.Handled)
            {
                return;
            }

            if (!used.TryGetComponent(out IActivate? activateComp))
            {
                return;
            }

            // all activates should only fire when in range / unbostructed
            var activateEventArgs = new ActivateEventArgs { User = user, Target = used };
            if (activateEventArgs.InRangeUnobstructed(ignoreInsideBlocker: true, popup: true))
            {
                activateComp.Activate(activateEventArgs);
            }
        }

        /// <summary>
        /// Uses an empty hand on an entity
        /// Finds components with the InteractHand interface and calls their function
        /// </summary>
        public void Interaction(IEntity user, IEntity attacked)
        {
            var message = new AttackHandMessage(user, attacked);
            RaiseLocalEvent(message);
            if (message.Handled)
            {
                return;
            }

            var attackHandEventArgs = new InteractHandEventArgs { User = user, Target = attacked };

            // all attackHands should only fire when in range / unobstructed
            if (attackHandEventArgs.InRangeUnobstructed(ignoreInsideBlocker: true, popup: true))
            {
                foreach (var attackHand in attacked.GetAllComponents<IInteractHand>())
                {
                    if (attackHand.InteractHand(attackHandEventArgs))
                    {
                        // If an InteractHand returns a status completion we finish our attack
                        return;
                    }
                }
            }

            // Else we run Activate.
            InteractionActivate(user, attacked);
        }

        /// <summary>
        /// Activates the Use behavior of an object
        /// Verifies that the user is capable of doing the use interaction first
        /// </summary>
        /// <param name="user"></param>
        /// <param name="used"></param>
        public void TryUseInteraction(IEntity? user, IEntity? used)
        {
            if (user != null && used != null && ActionBlockerSystem.CanUse(user))
            {
                UseInteraction(user, used);
            }
        }

        /// <summary>
        /// Activates/Uses an object in control/possession of a user
        /// If the item has the IUse interface on one of its components we use the object in our hand
        /// </summary>
        private void UseInteraction(IEntity user, IEntity used)
        {
            if (used.TryGetComponent<SharedUseDelayComponent>(out var delayComponent))
            {
                if (delayComponent.ActiveDelay)
                    return;
                else
                    delayComponent.BeginDelay();
            }

            var useMsg = new UseInHandMessage(user, used);
            RaiseLocalEvent(useMsg);
            if (useMsg.Handled)
            {
                return;
            }

            // Try to use item on any components which have the interface
            foreach (var use in used.GetAllComponents<IUse>())
            {
                if (use.UseEntity(new UseEntityEventArgs { User = user }))
                {
                    // If a Use returns a status completion we finish our attack
                    return;
                }
            }
        }

        private bool HandleWideAttack(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
        {
            // client sanitization
            if (!_mapManager.GridExists(coords.GetGridId(EntityManager)))
            {
                Logger.InfoS("system.interaction", $"Invalid Coordinates: client={session}, coords={coords}");
                return true;
            }

            if (uid.IsClientSide())
            {
                Logger.WarningS("system.interaction",
                    $"Client sent attack with client-side entity. Session={session}, Uid={uid}");
                return true;
            }

            var userEntity = session?.AttachedEntity;

            if (userEntity == null || !userEntity.IsValid())
            {
                return true;
            }

            if (userEntity.TryGetComponent(out SharedCombatModeComponent? combatMode) && combatMode.IsInCombatMode)
            {
                DoAttack(userEntity, coords, true);
            }

            return true;
        }

        protected void DoAttack(IEntity player, EntityCoordinates coordinates, bool wideAttack, EntityUid target = default)
        {
            // Verify player is on the same map as the entity he clicked on
            if (_mapManager.GetGrid(coordinates.GetGridId(EntityManager)).ParentMapId != player.Transform.MapID)
            {
                Logger.WarningS("system.interaction",
                    $"Player named {player.Name} clicked on a map he isn't located on");
                return;
            }

            if (!ActionBlockerSystem.CanAttack(player) ||
                (!wideAttack && !player.InRangeUnobstructed(coordinates, ignoreInsideBlocker: true)))
            {
                return;
            }

            var eventArgs = new AttackEventArgs(player, coordinates, wideAttack, target);

            // Verify player has a hand, and find what object he is currently holding in his active hand
            if (player.TryGetComponent<SharedHandsComponent>(out var hands))
            {
                var item = hands.GetActiveHand?.Owner;

                if (item != null)
                {
                    foreach (var attackComponent in item.GetAllComponents<IAttack>())
                    {
                        if (wideAttack ? attackComponent.WideAttack(eventArgs) : attackComponent.ClickAttack(eventArgs))
                            return;
                    }
                }
                else
                {
                    // We pick up items if our hand is empty, even if we're in combat mode.
                    if(EntityManager.TryGetEntity(target, out var targetEnt))
                    {
                        if (targetEnt.HasComponent<SharedItemComponent>())
                        {
                            Interaction(player, targetEnt);
                            return;
                        }
                    }
                }
            }

            foreach (var attackComponent in player.GetAllComponents<IAttack>())
            {
                if (wideAttack)
                    attackComponent.WideAttack(eventArgs);
                else
                    attackComponent.ClickAttack(eventArgs);
            }
        }

        /// <summary>
        /// Will have two behaviors, either "uses" the weapon at range on the entity if it is capable of accepting that action
        /// Or it will use the weapon itself on the position clicked, regardless of what was there
        /// </summary>
        private void RangedInteraction(IEntity user, IEntity weapon, IEntity attacked, EntityCoordinates clickLocation)
        {
            var rangedMsg = new RangedInteractMessage(user, weapon, attacked, clickLocation);
            RaiseLocalEvent(rangedMsg);
            if (rangedMsg.Handled)
                return;

            var rangedAttackByEventArgs = new RangedInteractEventArgs
            {
                User = user, Using = weapon, ClickLocation = clickLocation
            };

            // See if we have a ranged attack interaction
            foreach (var t in attacked.GetAllComponents<IRangedInteract>())
            {
                if (t.RangedInteract(rangedAttackByEventArgs))
                {
                    // If an InteractUsing returns a status completion we finish our attack
                    return;
                }
            }

            var afterAtkMsg = new AfterInteractMessage(user, weapon, attacked, clickLocation, false);
            RaiseLocalEvent(afterAtkMsg);
            if (afterAtkMsg.Handled)
                return;

            var afterAttackEventArgs = new AfterInteractEventArgs
            {
                User = user, ClickLocation = clickLocation, Target = attacked, CanReach = false
            };

            //See if we have a ranged attack interaction
            foreach (var afterAttack in weapon.GetAllComponents<IAfterInteract>())
            {
                afterAttack.AfterInteract(afterAttackEventArgs);
            }
        }

        /// <summary>
        ///     Calls HandSelected on all components that implement the IHandSelected interface
        ///     on an item entity on a hand that has just been selected.
        /// </summary>
        public void HandSelectedInteraction(IEntity user, IEntity item)
        {
            var handSelectedMsg = new HandSelectedMessage(user, item);
            RaiseLocalEvent(handSelectedMsg);
            if (handSelectedMsg.Handled)
            {
                return;
            }

            // Call Land on all components that implement the interface
            foreach (var comp in item.GetAllComponents<IHandSelected>())
            {
                comp.HandSelected(new HandSelectedEventArgs(user));
            }
        }

        /// <summary>
        /// Activates the Throw behavior of an object
        /// Verifies that the user is capable of doing the throw interaction first
        /// </summary>
        public bool TryThrowInteraction(IEntity? user, IEntity? item)
        {
            if (user == null || item == null || !ActionBlockerSystem.CanThrow(user)) return false;

            ThrownInteraction(user, item);
            return true;
        }

        /// <summary>
        ///     Calls Thrown on all components that implement the IThrown interface
        ///     on an entity that has been thrown.
        /// </summary>
        private void ThrownInteraction(IEntity user, IEntity thrown)
        {
            var throwMsg = new ThrownMessage(user, thrown);
            RaiseLocalEvent(throwMsg);
            if (throwMsg.Handled)
            {
                return;
            }

            // Call Thrown on all components that implement the interface
            foreach (var comp in thrown.GetAllComponents<IThrown>())
            {
                comp.Thrown(new ThrownEventArgs(user));
            }
        }

        /// <summary>
        ///     Calls Land on all components that implement the ILand interface
        ///     on an entity that has landed after being thrown.
        /// </summary>
        public void LandInteraction(IEntity user, IEntity landing, EntityCoordinates landLocation)
        {
            var landMsg = new LandMessage(user, landing, landLocation);
            RaiseLocalEvent(landMsg);
            if (landMsg.Handled)
            {
                return;
            }

            // Call Land on all components that implement the interface
            foreach (var comp in landing.GetAllComponents<ILand>())
            {
                comp.Land(new LandEventArgs(user, landLocation));
            }
        }

        /// <summary>
        ///     Calls ThrowCollide on all components that implement the IThrowCollide interface
        ///     on a thrown entity and the target entity it hit.
        /// </summary>
        public void ThrowCollideInteraction(IEntity user, IEntity thrown, IEntity target, EntityCoordinates location)
        {
            var collideMsg = new ThrowCollideMessage(user, thrown, target, location);
            RaiseLocalEvent(collideMsg);
            if (collideMsg.Handled)
            {
                return;
            }

            var eventArgs = new ThrowCollideEventArgs(user, thrown, target, location);

            foreach (var comp in thrown.GetAllComponents<IThrowCollide>().ToArray())
            {
                comp.DoHit(eventArgs);
            }

            foreach (var comp in target.GetAllComponents<IThrowCollide>().ToArray())
            {
                comp.HitBy(eventArgs);
            }
        }

        /// <summary>
        ///     Calls Equipped on all components that implement the IEquipped interface
        ///     on an entity that has been equipped.
        /// </summary>
        public void EquippedInteraction(IEntity user, IEntity equipped, EquipmentSlotDefines.Slots slot)
        {
            var equipMsg = new EquippedMessage(user, equipped, slot);
            RaiseLocalEvent(equipMsg);
            if (equipMsg.Handled)
            {
                return;
            }

            // Call Thrown on all components that implement the interface
            foreach (var comp in equipped.GetAllComponents<IEquipped>())
            {
                comp.Equipped(new EquippedEventArgs(user, slot));
            }
        }

        /// <summary>
        ///     Calls Unequipped on all components that implement the IUnequipped interface
        ///     on an entity that has been equipped.
        /// </summary>
        public void UnequippedInteraction(IEntity user, IEntity equipped, EquipmentSlotDefines.Slots slot)
        {
            var unequipMsg = new UnequippedMessage(user, equipped, slot);
            RaiseLocalEvent(unequipMsg);
            if (unequipMsg.Handled)
            {
                return;
            }

            // Call Thrown on all components that implement the interface
            foreach (var comp in equipped.GetAllComponents<IUnequipped>())
            {
                comp.Unequipped(new UnequippedEventArgs(user, slot));
            }
        }

        /// <summary>
        /// Activates the Dropped behavior of an object
        /// Verifies that the user is capable of doing the drop interaction first
        /// </summary>
        public bool TryDroppedInteraction(IEntity? user, IEntity? item)
        {
            if (user == null || item == null || !ActionBlockerSystem.CanDrop(user)) return false;

            DroppedInteraction(user, item);
            return true;
        }

        /// <summary>
        ///     Calls Dropped on all components that implement the IDropped interface
        ///     on an entity that has been dropped.
        /// </summary>
        public void DroppedInteraction(IEntity user, IEntity item)
        {
            var dropMsg = new DroppedMessage(user, item);
            RaiseLocalEvent(dropMsg);
            if (dropMsg.Handled)
            {
                return;
            }

            // Call Land on all components that implement the interface
            foreach (var comp in item.GetAllComponents<IDropped>())
            {
                comp.Dropped(new DroppedEventArgs(user));
            }
        }

        /// <summary>
        ///     Calls HandDeselected on all components that implement the IHandDeselected interface
        ///     on an item entity on a hand that has just been deselected.
        /// </summary>
        public void HandDeselectedInteraction(IEntity user, IEntity item)
        {
            var handDeselectedMsg = new HandDeselectedMessage(user, item);
            RaiseLocalEvent(handDeselectedMsg);
            if (handDeselectedMsg.Handled)
            {
                return;
            }

            // Call Land on all components that implement the interface
            foreach (var comp in item.GetAllComponents<IHandDeselected>())
            {
                comp.HandDeselected(new HandDeselectedEventArgs(user));
            }
        }

        private bool HandleClientUseItemInHand(ICommonSession? session, EntityCoordinates coords,
            EntityUid uid)
        {
            // client sanitization
            if (!_mapManager.GridExists(coords.GetGridId(EntityManager)))
            {
                Logger.InfoS("system.interaction", $"Invalid Coordinates: client={session}, coords={coords}");
                return true;
            }

            if (uid.IsClientSide())
            {
                Logger.WarningS("system.interaction",
                    $"Client sent interaction with client-side entity. Session={session}, Uid={uid}");
                return true;
            }

            var userEntity = session?.AttachedEntity;

            if (userEntity == null || !userEntity.IsValid())
            {
                return true;
            }

            if (userEntity.TryGetComponent(out SharedCombatModeComponent? combat) && combat.IsInCombatMode)
                DoAttack(userEntity, coords, false, uid);
            else
                UserInteraction(userEntity, coords, uid);

            return true;
        }

        protected void UserInteraction(IEntity player, EntityCoordinates coordinates, EntityUid clickedUid)
        {
            // Get entity clicked upon from UID if valid UID, if not assume no entity clicked upon and null
            if (!EntityManager.TryGetEntity(clickedUid, out var attacked))
            {
                attacked = null;
            }

            // Verify player has a transform component
            if (!player.TryGetComponent<ITransformComponent>(out var playerTransform))
            {
                return;
            }

            // Verify player is on the same map as the entity he clicked on
            if (_mapManager.GetGrid(coordinates.GetGridId(EntityManager)).ParentMapId != playerTransform.MapID)
            {
                Logger.WarningS("system.interaction",
                    $"Player named {player.Name} clicked on a map he isn't located on");
                return;
            }

            // Verify player has a hand, and find what object he is currently holding in his active hand
            if (!player.TryGetComponent<SharedHandsComponent>(out var hands))
            {
                return;
            }

            var item = hands.GetActiveHand?.Owner;

            if (ActionBlockerSystem.CanChangeDirection(player))
            {
                var diff = coordinates.ToMapPos(EntityManager) - playerTransform.MapPosition.Position;
                if (diff.LengthSquared > 0.01f)
                {
                    playerTransform.LocalRotation = new Angle(diff);
                }
            }

            if (!ActionBlockerSystem.CanInteract(player))
            {
                return;
            }

            // If in a container
            if (ContainerHelpers.IsInContainer(player))
            {
                return;
            }

            // In a container where the attacked entity is not the container's owner
            if (ContainerHelpers.TryGetContainer(player, out var playerContainer) &&
                attacked != playerContainer.Owner)
            {
                // Either the attacked entity is null, not contained or in a different container
                if (attacked == null ||
                    !ContainerHelpers.TryGetContainer(attacked, out var attackedContainer) ||
                    attackedContainer != playerContainer)
                {
                    return;
                }
            }

            // TODO: Check if client should be able to see that object to click on it in the first place

            // Clicked on empty space behavior, try using ranged attack
            if (attacked == null)
            {
                if (item != null)
                {
                    // After attack: Check if we clicked on an empty location, if so the only interaction we can do is AfterInteract
                    var distSqrt = (playerTransform.WorldPosition - coordinates.ToMapPos(EntityManager)).LengthSquared;
                    InteractAfter(player, item, coordinates, distSqrt <= InteractionRangeSquared);
                }

                return;
            }

            // Verify attacked object is on the map if we managed to click on it somehow
            if (!attacked.Transform.IsMapTransform)
            {
                Logger.WarningS("system.interaction",
                    $"Player named {player.Name} clicked on object {attacked.Name} that isn't currently on the map somehow");
                return;
            }

            // RangedInteract/AfterInteract: Check distance between user and clicked item, if too large parse it in the ranged function
            // TODO: have range based upon the item being used? or base it upon some variables of the player himself?
            var distance = (playerTransform.WorldPosition - attacked.Transform.WorldPosition).LengthSquared;
            if (distance > InteractionRangeSquared)
            {
                if (item != null)
                {
                    RangedInteraction(player, item, attacked, coordinates);
                    return;
                }

                return; // Add some form of ranged InteractHand here if you need it someday, or perhaps just ways to modify the range of InteractHand
            }

            // We are close to the nearby object and the object isn't contained in our active hand
            // InteractUsing/AfterInteract: We will either use the item on the nearby object
            if (item != null)
            {
                _ = Interaction(player, item, attacked, coordinates);
            }
            // InteractHand/Activate: Since our hand is empty we will use InteractHand/Activate
            else
            {
                Interaction(player, attacked);
            }
        }

        /// <summary>
        ///     We didn't click on any entity, try doing an AfterInteract on the click location
        /// </summary>
        private void InteractAfter(IEntity user, IEntity weapon, EntityCoordinates clickLocation, bool canReach)
        {
            var message = new AfterInteractMessage(user, weapon, null, clickLocation, canReach);
            RaiseLocalEvent(message);
            if (message.Handled)
            {
                return;
            }

            var afterInteractEventArgs = new AfterInteractEventArgs { User = user, ClickLocation = clickLocation, CanReach = canReach };

            foreach (var afterInteract in weapon.GetAllComponents<IAfterInteract>())
            {
                afterInteract.AfterInteract(afterInteractEventArgs);
            }
        }

        /// <summary>
        /// Uses a weapon/object on an entity
        /// Finds components with the InteractUsing interface and calls their function
        /// </summary>
        public async Task Interaction(IEntity user, IEntity weapon, IEntity attacked, EntityCoordinates clickLocation)
        {
            var attackMsg = new InteractUsingMessage(user, weapon, attacked, clickLocation);
            RaiseLocalEvent(attackMsg);
            if (attackMsg.Handled)
            {
                return;
            }

            var attackBys = attacked.GetAllComponents<IInteractUsing>().OrderByDescending(x => x.Priority);
            var attackByEventArgs = new InteractUsingEventArgs
            {
                User = user, ClickLocation = clickLocation, Using = weapon, Target = attacked
            };

            // all AttackBys should only happen when in range / unobstructed, so no range check is needed
            if (attackByEventArgs.InRangeUnobstructed(ignoreInsideBlocker: true, popup: true))
            {
                foreach (var attackBy in attackBys)
                {
                    if (await attackBy.InteractUsing(attackByEventArgs))
                    {
                        // If an InteractUsing returns a status completion we finish our attack
                        return;
                    }
                }
            }

            var afterAtkMsg = new AfterInteractMessage(user, weapon, attacked, clickLocation, true);
            RaiseLocalEvent(afterAtkMsg);
            if (afterAtkMsg.Handled)
            {
                return;
            }

            // If we aren't directly attacking the nearby object, lets see if our item has an after attack we can do
            var afterAttackEventArgs = new AfterInteractEventArgs
            {
                User = user, ClickLocation = clickLocation, Target = attacked, CanReach = true
            };

            foreach (var afterAttack in weapon.GetAllComponents<IAfterInteract>())
            {
                afterAttack.AfterInteract(afterAttackEventArgs);
            }
        }

        protected void HandleDragDropMessage(DragDropMessage msg, EntitySessionEventArgs args)
        {
            var performer = args.SenderSession.AttachedEntity;
            if (performer == null || !EntityManager.TryGetEntity(msg.Dropped, out var dropped)) return;
            if (!EntityManager.TryGetEntity(msg.Target, out var target)) return;

            var interactionArgs = new DragDropEventArgs(performer, msg.DropLocation, dropped, target);

            // must be in range of both the target and the object they are drag / dropping
            if (!interactionArgs.InRangeUnobstructed(ignoreInsideBlocker: true, popup: true)) return;

            // trigger dragdrops on the dropped entity
            foreach (var dragDrop in dropped.GetAllComponents<IDraggable>())
            {
                if (dragDrop.CanDrop(interactionArgs) &&
                    dragDrop.Drop(interactionArgs))
                {
                    return;
                }
            }

            // trigger dragdropons on the targeted entity
            foreach (var dragDropOn in target.GetAllComponents<IDragDropOn>())
            {
                if (dragDropOn.CanDragDropOn(interactionArgs) &&
                    dragDropOn.DragDropOn(interactionArgs))
                {
                    return;
                }
            }
        }

        // Smug's 50 billion helpers below

        /// <summary>
        ///     Traces a ray from coords to otherCoords and returns the length
        ///     of the vector between coords and the ray's first hit.
        /// </summary>
        /// <param name="origin">Set of coordinates to use.</param>
        /// <param name="other">Other set of coordinates to use.</param>
        /// <param name="collisionMask">the mask to check for collisions</param>
        /// <param name="predicate">
        ///     A predicate to check whether to ignore an entity or not.
        ///     If it returns true, it will be ignored.
        /// </param>
        /// <returns>Length of resulting ray.</returns>
        public float UnobstructedDistance(
            MapCoordinates origin,
            MapCoordinates other,
            int collisionMask = (int) CollisionGroup.Impassable,
            Ignored? predicate = null)
        {
            var dir = other.Position - origin.Position;

            if (dir.LengthSquared.Equals(0f)) return 0f;

            predicate ??= _ => false;
            var ray = new CollisionRay(origin.Position, dir.Normalized, collisionMask);
            var rayResults = _physicsManager.IntersectRayWithPredicate(origin.MapId, ray, dir.Length, predicate.Invoke, false).ToList();

            if (rayResults.Count == 0) return dir.Length;
            return (rayResults[0].HitPos - origin.Position).Length;
        }

        /// <summary>
        ///     Traces a ray from coords to otherCoords and returns the length
        ///     of the vector between coords and the ray's first hit.
        /// </summary>
        /// <param name="origin">Set of coordinates to use.</param>
        /// <param name="other">Other set of coordinates to use.</param>
        /// <param name="collisionMask">The mask to check for collisions</param>
        /// <param name="ignoredEnt">
        ///     The entity to be ignored when checking for collisions.
        /// </param>
        /// <returns>Length of resulting ray.</returns>
        public float UnobstructedDistance(
            MapCoordinates origin,
            MapCoordinates other,
            int collisionMask = (int) CollisionGroup.Impassable,
            IEntity? ignoredEnt = null)
        {
            var predicate = ignoredEnt == null
                ? null
                : (Ignored) (e => e == ignoredEnt);

            return UnobstructedDistance(origin, other, collisionMask, predicate);
        }

        /// <summary>
        ///     Checks that these coordinates are within a certain distance without any
        ///     entity that matches the collision mask obstructing them.
        ///     If the <paramref name="range"/> is zero or negative,
        ///     this method will only check if nothing obstructs the two sets
        ///     of coordinates.
        /// </summary>
        /// <param name="origin">Set of coordinates to use.</param>
        /// <param name="other">Other set of coordinates to use.</param>
        /// <param name="range">
        ///     Maximum distance between the two sets of coordinates.
        /// </param>
        /// <param name="collisionMask">The mask to check for collisions.</param>
        /// <param name="predicate">
        ///     A predicate to check whether to ignore an entity or not.
        ///     If it returns true, it will be ignored.
        /// </param>
        /// <param name="ignoreInsideBlocker">
        ///     If true and <see cref="origin"/> or <see cref="other"/> are inside
        ///     the obstruction, ignores the obstruction and considers the interaction
        ///     unobstructed.
        ///     Therefore, setting this to true makes this check more permissive,
        ///     such as allowing an interaction to occur inside something impassable
        ///     (like a wall). The default, false, makes the check more restrictive.
        /// </param>
        /// <returns>
        ///     True if the two points are within a given range without being obstructed.
        /// </returns>
        public bool InRangeUnobstructed(
            MapCoordinates origin,
            MapCoordinates other,
            float range = InteractionRange,
            CollisionGroup collisionMask = CollisionGroup.Impassable,
            Ignored? predicate = null,
            bool ignoreInsideBlocker = false)
        {
            if (!origin.InRange(other, range)) return false;

            var dir = other.Position - origin.Position;

            if (dir.LengthSquared.Equals(0f)) return true;
            if (range > 0f && !(dir.LengthSquared <= range * range)) return false;

            predicate ??= _ => false;

            var ray = new CollisionRay(origin.Position, dir.Normalized, (int) collisionMask);
            var rayResults = _physicsManager.IntersectRayWithPredicate(origin.MapId, ray, dir.Length, predicate.Invoke, false).ToList();

            if (rayResults.Count == 0) return true;

            if (!ignoreInsideBlocker) return false;

            if (rayResults.Count <= 0) return false;

            return (rayResults[0].HitPos - other.Position).Length < 1f;
        }

        /// <summary>
        ///     Checks that two entities are within a certain distance without any
        ///     entity that matches the collision mask obstructing them.
        ///     If the <paramref name="range"/> is zero or negative,
        ///     this method will only check if nothing obstructs the two entities.
        /// </summary>
        /// <param name="origin">The first entity to use.</param>
        /// <param name="other">Other entity to use.</param>
        /// <param name="range">
        ///     Maximum distance between the two entities.
        /// </param>
        /// <param name="collisionMask">The mask to check for collisions.</param>
        /// <param name="predicate">
        ///     A predicate to check whether to ignore an entity or not.
        ///     If it returns true, it will be ignored.
        /// </param>
        /// <param name="ignoreInsideBlocker">
        ///     If true and <see cref="origin"/> or <see cref="other"/> are inside
        ///     the obstruction, ignores the obstruction and considers the interaction
        ///     unobstructed.
        ///     Therefore, setting this to true makes this check more permissive,
        ///     such as allowing an interaction to occur inside something impassable
        ///     (like a wall). The default, false, makes the check more restrictive.
        /// </param>
        /// <param name="popup">
        ///     Whether or not to popup a feedback message on the origin entity for
        ///     it to see.
        /// </param>
        /// <returns>
        ///     True if the two points are within a given range without being obstructed.
        /// </returns>
        public bool InRangeUnobstructed(
            IEntity origin,
            IEntity other,
            float range = InteractionRange,
            CollisionGroup collisionMask = CollisionGroup.Impassable,
            Ignored? predicate = null,
            bool ignoreInsideBlocker = false,
            bool popup = false)
        {
            var originPosition = origin.Transform.MapPosition;
            var otherPosition = other.Transform.MapPosition;
            predicate ??= e => e == origin || e == other;

            var inRange = InRangeUnobstructed(originPosition, otherPosition, range, collisionMask, predicate, ignoreInsideBlocker);

            if (!inRange && popup)
            {
                var message = Loc.GetString("You can't reach there!");
                origin.PopupMessage(message);
            }

            return inRange;
        }

        /// <summary>
        ///     Checks that an entity and a component are within a certain
        ///     distance without any entity that matches the collision mask
        ///     obstructing them.
        ///     If the <paramref name="range"/> is zero or negative,
        ///     this method will only check if nothing obstructs the entity and component.
        /// </summary>
        /// <param name="origin">The entity to use.</param>
        /// <param name="other">The component to use.</param>
        /// <param name="range">
        ///     Maximum distance between the entity and component.
        /// </param>
        /// <param name="collisionMask">The mask to check for collisions.</param>
        /// <param name="predicate">
        ///     A predicate to check whether to ignore an entity or not.
        ///     If it returns true, it will be ignored.
        /// </param>
        /// <param name="ignoreInsideBlocker">
        ///     If true and <see cref="origin"/> or <see cref="other"/> are inside
        ///     the obstruction, ignores the obstruction and considers the interaction
        ///     unobstructed.
        ///     Therefore, setting this to true makes this check more permissive,
        ///     such as allowing an interaction to occur inside something impassable
        ///     (like a wall). The default, false, makes the check more restrictive.
        /// </param>
        /// <param name="popup">
        ///     Whether or not to popup a feedback message on the origin entity for
        ///     it to see.
        /// </param>
        /// <returns>
        ///     True if the two points are within a given range without being obstructed.
        /// </returns>
        public bool InRangeUnobstructed(
            IEntity origin,
            IComponent other,
            float range = InteractionRange,
            CollisionGroup collisionMask = CollisionGroup.Impassable,
            Ignored? predicate = null,
            bool ignoreInsideBlocker = false,
            bool popup = false)
        {
            var originPosition = origin.Transform.MapPosition;
            var otherPosition = other.Owner.Transform.MapPosition;
            predicate ??= e => e == origin || e == other.Owner;

            var inRange = InRangeUnobstructed(originPosition, otherPosition, range, collisionMask, predicate, ignoreInsideBlocker);

            if (!inRange && popup)
            {
                var message = Loc.GetString("You can't reach there!");
                origin.PopupMessage(message);
            }

            return inRange;
        }

        /// <summary>
        ///     Checks that an entity and a set of grid coordinates are within a certain
        ///     distance without any entity that matches the collision mask
        ///     obstructing them.
        ///     If the <paramref name="range"/> is zero or negative,
        ///     this method will only check if nothing obstructs the entity and component.
        /// </summary>
        /// <param name="origin">The entity to use.</param>
        /// <param name="other">The grid coordinates to use.</param>
        /// <param name="range">
        ///     Maximum distance between the two entity and set of grid coordinates.
        /// </param>
        /// <param name="collisionMask">The mask to check for collisions.</param>
        /// <param name="predicate">
        ///     A predicate to check whether to ignore an entity or not.
        ///     If it returns true, it will be ignored.
        /// </param>
        /// <param name="ignoreInsideBlocker">
        ///     If true and <see cref="origin"/> or <see cref="other"/> are inside
        ///     the obstruction, ignores the obstruction and considers the interaction
        ///     unobstructed.
        ///     Therefore, setting this to true makes this check more permissive,
        ///     such as allowing an interaction to occur inside something impassable
        ///     (like a wall). The default, false, makes the check more restrictive.
        /// </param>
        /// <param name="popup">
        ///     Whether or not to popup a feedback message on the origin entity for
        ///     it to see.
        /// </param>
        /// <returns>
        ///     True if the two points are within a given range without being obstructed.
        /// </returns>
        public bool InRangeUnobstructed(
            IEntity origin,
            EntityCoordinates other,
            float range = InteractionRange,
            CollisionGroup collisionMask = CollisionGroup.Impassable,
            Ignored? predicate = null,
            bool ignoreInsideBlocker = false,
            bool popup = false)
        {
            var originPosition = origin.Transform.MapPosition;
            var otherPosition = other.ToMap(EntityManager);
            predicate ??= e => e == origin;

            var inRange = InRangeUnobstructed(originPosition, otherPosition, range, collisionMask, predicate, ignoreInsideBlocker);

            if (!inRange && popup)
            {
                var message = Loc.GetString("You can't reach there!");
                origin.PopupMessage(message);
            }

            return inRange;
        }

        /// <summary>
        ///     Checks that an entity and a set of map coordinates are within a certain
        ///     distance without any entity that matches the collision mask
        ///     obstructing them.
        ///     If the <paramref name="range"/> is zero or negative,
        ///     this method will only check if nothing obstructs the entity and component.
        /// </summary>
        /// <param name="origin">The entity to use.</param>
        /// <param name="other">The map coordinates to use.</param>
        /// <param name="range">
        ///     Maximum distance between the two entity and set of map coordinates.
        /// </param>
        /// <param name="collisionMask">The mask to check for collisions.</param>
        /// <param name="predicate">
        ///     A predicate to check whether to ignore an entity or not.
        ///     If it returns true, it will be ignored.
        /// </param>
        /// <param name="ignoreInsideBlocker">
        ///     If true and <see cref="origin"/> or <see cref="other"/> are inside
        ///     the obstruction, ignores the obstruction and considers the interaction
        ///     unobstructed.
        ///     Therefore, setting this to true makes this check more permissive,
        ///     such as allowing an interaction to occur inside something impassable
        ///     (like a wall). The default, false, makes the check more restrictive.
        /// </param>
        /// <param name="popup">
        ///     Whether or not to popup a feedback message on the origin entity for
        ///     it to see.
        /// </param>
        /// <returns>
        ///     True if the two points are within a given range without being obstructed.
        /// </returns>
        public bool InRangeUnobstructed(
            IEntity origin,
            MapCoordinates other,
            float range = InteractionRange,
            CollisionGroup collisionMask = CollisionGroup.Impassable,
            Ignored? predicate = null,
            bool ignoreInsideBlocker = false,
            bool popup = false)
        {
            var originPosition = origin.Transform.MapPosition;
            predicate ??= e => e == origin;

            var inRange = InRangeUnobstructed(originPosition, other, range, collisionMask, predicate, ignoreInsideBlocker);

            if (!inRange && popup)
            {
                var message = Loc.GetString("You can't reach there!");
                origin.PopupMessage(message);
            }

            return inRange;
        }

        /// <summary>
        ///     Checks that the user and target of a
        ///     <see cref="ITargetedInteractEventArgs"/> are within a certain
        ///     distance without any entity that matches the collision mask
        ///     obstructing them.
        ///     If the <paramref name="range"/> is zero or negative,
        ///     this method will only check if nothing obstructs the entity and component.
        /// </summary>
        /// <param name="args">The event args to use.</param>
        /// <param name="range">
        ///     Maximum distance between the two entity and set of map coordinates.
        /// </param>
        /// <param name="collisionMask">The mask to check for collisions.</param>
        /// <param name="predicate">
        ///     A predicate to check whether to ignore an entity or not.
        ///     If it returns true, it will be ignored.
        /// </param>
        /// <param name="ignoreInsideBlocker">
        ///     If true and both the user and target are inside
        ///     the obstruction, ignores the obstruction and considers the interaction
        ///     unobstructed.
        ///     Therefore, setting this to true makes this check more permissive,
        ///     such as allowing an interaction to occur inside something impassable
        ///     (like a wall). The default, false, makes the check more restrictive.
        /// </param>
        /// <param name="popup">
        ///     Whether or not to popup a feedback message on the user entity for
        ///     it to see.
        /// </param>
        /// <returns>
        ///     True if the two points are within a given range without being obstructed.
        /// </returns>
        public bool InRangeUnobstructed(
            ITargetedInteractEventArgs args,
            float range = InteractionRange,
            CollisionGroup collisionMask = CollisionGroup.Impassable,
            Ignored? predicate = null,
            bool ignoreInsideBlocker = false,
            bool popup = false)
        {
            var origin = args.User;
            var other = args.Target;

            return InRangeUnobstructed(origin, other, range, collisionMask, predicate, ignoreInsideBlocker, popup);
        }

        /// <summary>
        ///     Checks that the user of a <see cref="DragDropEventArgs"/> is within a
        ///     certain distance of the target and dropped entities without any entity
        ///     that matches the collision mask obstructing them.
        ///     If the <paramref name="range"/> is zero or negative,
        ///     this method will only check if nothing obstructs the entity and component.
        /// </summary>
        /// <param name="args">The event args to use.</param>
        /// <param name="range">
        ///     Maximum distance between the two entity and set of map coordinates.
        /// </param>
        /// <param name="collisionMask">The mask to check for collisions.</param>
        /// <param name="predicate">
        ///     A predicate to check whether to ignore an entity or not.
        ///     If it returns true, it will be ignored.
        /// </param>
        /// <param name="ignoreInsideBlocker">
        ///     If true and both the user and target are inside
        ///     the obstruction, ignores the obstruction and considers the interaction
        ///     unobstructed.
        ///     Therefore, setting this to true makes this check more permissive,
        ///     such as allowing an interaction to occur inside something impassable
        ///     (like a wall). The default, false, makes the check more restrictive.
        /// </param>
        /// <param name="popup">
        ///     Whether or not to popup a feedback message on the user entity for
        ///     it to see.
        /// </param>
        /// <returns>
        ///     True if the two points are within a given range without being obstructed.
        /// </returns>
        public bool InRangeUnobstructed(
            DragDropEventArgs args,
            float range = InteractionRange,
            CollisionGroup collisionMask = CollisionGroup.Impassable,
            Ignored? predicate = null,
            bool ignoreInsideBlocker = false,
            bool popup = false)
        {
            var user = args.User;
            var dropped = args.Dragged;
            var target = args.Target;

            if (!InRangeUnobstructed(user, target, range, collisionMask, predicate, ignoreInsideBlocker))
            {
                if (popup)
                {
                    var message = Loc.GetString("You can't reach there!");
                    target.PopupMessage(user, message);
                }

                return false;
            }

            if (!InRangeUnobstructed(user, dropped, range, collisionMask, predicate, ignoreInsideBlocker))
            {
                if (popup)
                {
                    var message = Loc.GetString("You can't reach there!");
                    dropped.PopupMessage(user, message);
                }

                return false;
            }

            return true;
        }

        /// <summary>
        ///     Checks that the user and target of a
        ///     <see cref="AfterInteractEventArgs"/> are within a  certain distance
        ///     without any entity that matches the collision mask obstructing them.
        ///     If the <paramref name="range"/> is zero or negative,
        ///     this method will only check if nothing obstructs the entity and component.
        /// </summary>
        /// <param name="args">The event args to use.</param>
        /// <param name="range">
        ///     Maximum distance between the two entity and set of map coordinates.
        /// </param>
        /// <param name="collisionMask">The mask to check for collisions.</param>
        /// <param name="predicate">
        ///     A predicate to check whether to ignore an entity or not.
        ///     If it returns true, it will be ignored.
        /// </param>
        /// <param name="ignoreInsideBlocker">
        ///     If true and both the user and target are inside
        ///     the obstruction, ignores the obstruction and considers the interaction
        ///     unobstructed.
        ///     Therefore, setting this to true makes this check more permissive,
        ///     such as allowing an interaction to occur inside something impassable
        ///     (like a wall). The default, false, makes the check more restrictive.
        /// </param>
        /// <param name="popup">
        ///     Whether or not to popup a feedback message on the user entity for
        ///     it to see.
        /// </param>
        /// <returns>
        ///     True if the two points are within a given range without being obstructed.
        /// </returns>
        public bool InRangeUnobstructed(
            AfterInteractEventArgs args,
            float range = InteractionRange,
            CollisionGroup collisionMask = CollisionGroup.Impassable,
            Ignored? predicate = null,
            bool ignoreInsideBlocker = false,
            bool popup = false)
        {
            var user = args.User;
            var target = args.Target;
            predicate ??= e => e == user;

            MapCoordinates otherPosition;

            if (target == null)
            {
                otherPosition = args.ClickLocation.ToMap(EntityManager);
            }
            else
            {
                otherPosition = target.Transform.MapPosition;
                predicate += e => e == target;
            }

            return InRangeUnobstructed(user, otherPosition, range, collisionMask, predicate, ignoreInsideBlocker, popup);
        }
    }
}
