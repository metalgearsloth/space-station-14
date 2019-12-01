using System;
using System.Collections.Generic;
using System.Timers;
using Content.Server.GameObjects.Components.Movement;
using Content.Server.GameObjects.Components.Pathfinding;
using Robust.Server.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.Interfaces.Random;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Timer = Robust.Shared.Timers.Timer;

namespace Content.Server.AI.Routines.Movers
{
    public abstract class BaseMoverAiRoutine : AiRoutine
    {
        // These are to do with the tile-to-tile movement
        public IReadOnlyCollection<TileRef> Route => _route;
        protected readonly Queue<TileRef> _route = new Queue<TileRef>();
        protected GridCoordinates NextGrid;

        // How close to the route X / Y (centre of tile, at least currently) before good enough.
        protected float TileTolerance = 0.1f;

        // Stuck checkers
        private DateTime _lastStuckCheck = DateTime.Now;
        protected GridCoordinates OurLastPosition;

        public bool Arrived => _arrived;
        protected bool _arrived = true;

#pragma warning disable 649
        [Dependency] protected readonly IMapManager _mapManager;
        [Dependency] protected readonly IPathfinder _pathfinder;
#pragma warning restore 649

        /// <summary>
        /// How close we need to be to the target position before stopping
        /// </summary>
        public float TargetProximity { get; set; } = 2.0f;

        // Anti-stuck measures. See the AntiStuck() method for more details
        private DateTime _lastStuckTime;
        private bool _isStuck = false;
        private Vector2 _antiStuckDirection = Vector2.Zero;
        protected AntiStuckMethods AntiStuckMethod = AntiStuckMethods.PhaseThrough;

        /// <summary>
        /// Handles the big business, e.g. moving to the next node, re-assessing path, etc.
        /// </summary>
        public abstract void HandleMovement();

        // TODO: I R dumb with generics
        // public abstract void GetRoute<T>(T target, float proximity = 0.0f) where T : IEntity, GridCoordinates;

        /// <summary>
        /// Called once when the routine is being setup
        /// </summary>
        /// <param name="owner"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public override void Setup(IEntity owner)
        {
            base.Setup(owner);
            IoCManager.InjectDependencies(this);
            if (!owner.HasComponent<AiControllerComponent>())
            {
                Logger.FatalS("ai_routine", "MovementAiRoutine must have a valid controller on the owner");
                throw new InvalidOperationException();
            }

            OurLastPosition = Owner.Transform.GridPosition;
        }

        /// <summary>
        /// Will try and wiggle around if it seems like we're stuck
        /// </summary>
        protected void AntiStuck()
        {
            // First check if we're still in a stuck state
            if (_isStuck)
            {
                switch (AntiStuckMethod)
                {
                    case AntiStuckMethods.Jiggle:
                        if ((DateTime.Now - _lastStuckTime).TotalSeconds < 1.0f)
                        {
                            Owner.TryGetComponent(out AiControllerComponent mover);
                            mover.VelocityDir = _antiStuckDirection.Normalized;
                            break;
                        }
                        // TODO: To do this or not to do this; clearly the old route is invalid...
                        // Maybe just generate a route back to the original route?
                        HaveArrived();
                        break;
                    case AntiStuckMethods.PhaseThrough:
                        if (Owner.TryGetComponent(out CollidableComponent collidableComponent))
                        {
                            // TODO: If something updates this this will fuck it
                            collidableComponent.IsHardCollidable = false;
                            Logger.DebugS("ai", $"{Owner} became stuck, turning off collidable temporarily");
                            Timer.Spawn(1000, () =>
                            {
                                collidableComponent.IsHardCollidable = true;
                                Logger.DebugS("ai", $"Anti-stuck turning back on collidable for {Owner}");
                            });
                        }
                        break;
                    case AntiStuckMethods.Teleport:
                        Owner.Transform.DetachParent();
                        Owner.Transform.GridPosition = NextGrid;
                        Logger.DebugS("ai", $"Teleported {Owner} to {NextGrid} for anti-stuck");
                        break;
                    default:
                        throw new InvalidOperationException();
                }

                _isStuck = false;
            }

            // Stuck check cooldown
            if (!((DateTime.Now - _lastStuckCheck).TotalSeconds > 5.0f))
            {
                return;
            }

            _lastStuckCheck = DateTime.Now;

            // Are we actually stuck
            if ((OurLastPosition.Position - Owner.Transform.GridPosition.Position).Length < TileTolerance)
            {
                _lastStuckTime = DateTime.Now;
                _isStuck = true;
                Logger.DebugS("ai", $"{Owner} is stuck at {Owner.Transform.GridPosition}");
            }

            OurLastPosition = Owner.Transform.GridPosition;
        }

        /// <summary>
        /// Tells this routine we don't need to keep moving
        /// Other routines can have their own criteria for movement so they can tell this to stop as well
        /// </summary>
        public void HaveArrived()
        {
            _route.Clear();
            _arrived = true;
            Owner.TryGetComponent(out AiControllerComponent mover);
            mover.VelocityDir = Vector2.Zero;
            Logger.DebugS("ai", $"AI {Owner} arrived at target");
        }

        public override void Update()
        {
            base.Update();
            HandleMovement();
        }
    }

    public enum AntiStuckMethods
    {
        Jiggle, // Just pick a random direction for a bit and hope for the best
        Teleport, // The Half-Life 2 method
        PhaseThrough, // Just makes it non-collidable
    }
}
