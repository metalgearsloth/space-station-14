#nullable enable
using System.Diagnostics.CodeAnalysis;
using Content.Shared.GameObjects.Components.Mobs.State;
using Content.Shared.GameObjects.Components.Movement;
using Content.Shared.GameObjects.Components.Pulling;
using Content.Shared.GameObjects.EntitySystems.ActionBlocker;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Broadphase;
using Robust.Shared.Physics.Controllers;

namespace Content.Shared.Physics.Controllers
{
    /// <summary>
    ///     Handles player and NPC mob movement.
    ///     NPCs are handled server-side only.
    /// </summary>
    public abstract class SharedMoverController : VirtualController
    {
        [Dependency] private readonly IMapManager _mapManager = default!;
        [Dependency] private readonly IPhysicsManager _physicsManager = default!;

        private SharedBroadPhaseSystem _broadPhaseSystem = default!;

        public override void Initialize()
        {
            base.Initialize();
            _broadPhaseSystem = EntitySystem.Get<SharedBroadPhaseSystem>();
        }

        /// <summary>
        ///     A generic kinematic mover for entities.
        /// </summary>
        protected void HandleKinematicMovement(IMoverComponent mover, PhysicsComponent physicsComponent)
        {
            var (walkDir, sprintDir) = mover.VelocityDir;

            // Regular movement.
            // Target velocity.
            var total = (walkDir * mover.CurrentWalkSpeed + sprintDir * mover.CurrentSprintSpeed);

            if (total != Vector2.Zero)
            {
                mover.Owner.Transform.LocalRotation = total.GetDir().ToAngle();
            }

            physicsComponent.LinearVelocity = total;
        }

        /// <summary>
        ///     Movement while considering actionblockers, weightlessness, etc.
        /// </summary>
        /// <param name="mover"></param>
        /// <param name="physicsComponent"></param>
        /// <param name="mobMover"></param>
        protected void HandleMobMovement(IMoverComponent mover, PhysicsComponent physicsComponent, IMobMoverComponent mobMover)
        {
            // TODO: Look at https://gameworksdocs.nvidia.com/PhysX/4.1/documentation/physxguide/Manual/CharacterControllers.html?highlight=controller as it has some advice on kinematic controllers?
            if (!UseMobMovement(_broadPhaseSystem, physicsComponent, out var weightless, out var touching, _physicsManager, _mapManager))
            {
                return;
            }

            var transform = mover.Owner.Transform;
            var (walkDir, sprintDir) = mover.VelocityDir;

            // Handle wall-pushes.
            if (weightless.Value)
            {
                // No gravity: is our entity touching anything?
                if (!touching.Value)
                {
                    transform.LocalRotation = physicsComponent.LinearVelocity.GetDir().ToAngle();
                    return;
                }
            }

            // Regular movement.
            // Target velocity.
            var total = walkDir * mover.CurrentWalkSpeed + sprintDir * mover.CurrentSprintSpeed;

            if (weightless.Value)
            {
                total *= mobMover.WeightlessStrength;
            }

            if (total != Vector2.Zero)
            {
                // This should have its event run during island solver soooo
                transform.DeferUpdates = true;
                transform.LocalRotation = total.GetDir().ToAngle();
                transform.DeferUpdates = false;
                HandleFootsteps(mover, mobMover);
            }

            physicsComponent.LinearVelocity = total;
        }

        /// <summary>
        /// Should we use the kinematic mob movement for this body?
        /// </summary>
        /// <param name="broadPhaseSystem"></param>
        /// <param name="body"></param>
        /// <param name="weightless">Cached weightlessness so this can be re-used given it's not exactly cheap to calculate.</param>
        /// <param name="aroundCollider">Cached whether we are next to a static body we can push off of for weightlessness</param>
        /// <param name="physicsManager"></param>
        /// <param name="mapManager"></param>
        /// <returns></returns>
        public static bool UseMobMovement(SharedBroadPhaseSystem broadPhaseSystem, PhysicsComponent body, [NotNullWhen(true)] out bool? weightless, [NotNullWhen(true)] out bool? aroundCollider, IPhysicsManager? physicsManager = null, IMapManager? mapManager = null)
        {
            var owner = body.Owner;

            // If we're in control of our body use the kinematic movement, otherwise no.
            // The most likely out is either BodyStatus.InAir or TryGet SharedPlayerMobMover
            if (body.BodyStatus != BodyStatus.OnGround ||
                !owner.TryGetComponent(out SharedPlayerMobMoverComponent? mover) ||
                !owner.HasComponent<IMobStateComponent>() ||
                !ActionBlockerSystem.CanMove(owner))
            {
                weightless = null;
                aroundCollider = null;
                return false;
            }

            // If we're not weightless or we're near a static body to push off of then use kinematic movement.
            weightless = owner.IsWeightless(physicsManager);

            if (!weightless.Value)
            {
                aroundCollider = true;
                return true;
            }

            aroundCollider = IsAroundCollider(broadPhaseSystem, owner.Transform, mover, body, mapManager);

            return aroundCollider.Value;
        }

        /// <summary>
        ///     Used for weightlessness to determine if we are near a wall.
        /// </summary>
        public static bool IsAroundCollider(SharedBroadPhaseSystem broadPhaseSystem, ITransformComponent transform, IMobMoverComponent mover, IPhysBody collider, IMapManager? mapManager = null)
        {
            mapManager ??= IoCManager.Resolve<IMapManager>();

            foreach (var otherCollider in broadPhaseSystem.GetCollidingEntities(transform.MapID, collider.GetWorldAABB(mapManager).Enlarged(mover.GrabRange)))
            {
                if (otherCollider == collider) continue; // Don't try to push off of yourself!

                // Only allow pushing off of anchored things that have collision.
                if (otherCollider.BodyType != BodyType.Static ||
                    (collider.CollisionMask & otherCollider.CollisionLayer) == 0 && (otherCollider.CollisionMask & collider.CollisionLayer) == 0 ||
                    (otherCollider.Entity.TryGetComponent(out SharedPullableComponent? pullable) && pullable.BeingPulled))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        // TODO: Need a predicted client version that only plays for our own entity and then have server-side ignore our session (for that entity only)
        protected virtual void HandleFootsteps(IMoverComponent mover, IMobMoverComponent mobMover) {}
    }
}
