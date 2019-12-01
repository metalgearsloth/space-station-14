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
        protected bool IsStuck;
        protected AntiStuckMethods AntiStuckMethod = AntiStuckMethods.Jiggle;

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

            // So the stuck checker doesn't immediately run
            OurLastPosition = default;
        }

        /// <summary>
        /// Will try and get around obstacles if stuck
        /// </summary>
        protected void AntiStuck()
        {
            // TODO: More work because these are sketchy

            // First check if we're still in a stuck state
            if (IsStuck)
            {
                switch (AntiStuckMethod)
                {
                    case AntiStuckMethods.Jiggle:
                        if ((DateTime.Now - _lastStuckTime).TotalSeconds > 1.0f)
                        {
                            var randomRange = IoCManager.Resolve<IRobustRandom>().Next(0, 359);
                            var angle = Angle.FromDegrees(randomRange);
                            Owner.TryGetComponent(out AiControllerComponent mover);
                            mover.VelocityDir = angle.ToVec().Normalized;
                            _lastStuckTime = DateTime.Now;
                            break;
                        }

                        IsStuck = false;
                        break;
                    case AntiStuckMethods.PhaseThrough:
                        if (Owner.TryGetComponent(out CollidableComponent collidableComponent))
                        {
                            // TODO: If something updates this this will fuck it
                            collidableComponent.IsHardCollidable = false;
                            Logger.DebugS("ai", $"{Owner} became stuck, turning off collidable temporarily");
                            IsStuck = false;
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
                        IsStuck = false;
                        Logger.DebugS("ai", $"Teleported {Owner} to {NextGrid} for anti-stuck");
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }

            // Stuck check cooldown
            if ((DateTime.Now - _lastStuckCheck).TotalSeconds < 1.0f)
            {
                return;
            }

            _lastStuckCheck = DateTime.Now;

            // Are we actually stuck
            if ((OurLastPosition.Position - Owner.Transform.GridPosition.Position).Length < TileTolerance)
            {
                _lastStuckTime = DateTime.Now;
                IsStuck = true;
                Logger.DebugS("ai", $"{Owner} is stuck at {Owner.Transform.GridPosition}");
            }

            // TODO: If we've been stuck say 5 seconds re-route?

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
