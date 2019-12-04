using System;
using System.Collections.Generic;
using Content.Server.GameObjects.Components.Movement;
using Content.Server.GameObjects.Components.Pathfinding;
using Robust.Server.AI;
using Robust.Server.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.GameObjects.Components;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.Interfaces.Random;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Timer = Robust.Shared.Timers.Timer;

namespace Content.Server.AI.Routines.Movers
{
    public abstract class BaseAiMover
    {
        // Percentage of walk / sprint speed to move at.
        // Should generally be 1.0 but some stuff like the idler might move slower.
        public float Speed
        {
            get => _speed;
            set
            {
                // TODO: Clamp
                if (_speed > 1.0f)
                {
                    _speed = 1.0f;
                    return;
                }

                // Not falling for that HL2 infinite negative speed
                if (_speed < 0.0f)
                {
                    _speed = 0.0f;
                    return;
                }

                _speed = value;
            }
        }

        private float _speed = 1.0f;

        public IEntity Owner { get; }

        // These are to do with the tile-to-tile movement
        public IReadOnlyCollection<TileRef> Route => _route;
        protected readonly Queue<TileRef> _route = new Queue<TileRef>();
        protected GridCoordinates NextGrid;

        // How close to the route X / Y (centre of tile, at least currently) before good enough.
        protected float TileTolerance = 0.2f;

        // Stuck checkers
        private float _stuckTimerRemaining = 2.0f;
        protected GridCoordinates OurLastPosition;
        private float _antiStuckTryTimer = 0.2f;

        public bool Arrived => _arrived;
        protected bool _arrived = true;

#pragma warning disable 649
        [Dependency] protected readonly IMapManager _mapManager;
        [Dependency] protected readonly IPathfinder _pathfinder;
#pragma warning restore 649

        /// <summary>
        /// How close we need to be to the target position
        /// </summary>
        public float Range { get; set; } = 2.0f;

        /// <summary>
        /// How close the pathfinder has to get. Generally this shouldn't need to be overridden.
        /// </summary>
        protected float PathfinderRange = 0.5f;

        // Anti-stuck measures. See the AntiStuck() method for more details
        private bool _tryingAntiStuck = false;
        public bool IsStuck => _isStuck;
        protected bool _isStuck;
        protected AntiStuckMethods AntiStuckMethod = AntiStuckMethods.PhaseThrough;

        /// <summary>
        /// Handles the big business, e.g. moving to the next node, re-assessing path, etc.
        /// </summary>
        public abstract void HandleMovement(float frameTime);

        public BaseAiMover(IEntity owner)
        {
            Owner = owner;
            IoCManager.InjectDependencies(this);
            if (!owner.HasComponent<AiControllerComponent>())
            {
                throw new InvalidOperationException("AI Mover must have controller");
            }
            OurLastPosition = default;
        }

        /// <summary>
        /// Will try and get around obstacles if stuck
        /// </summary>
        protected void AntiStuck(float frameTime)
        {
            // TODO: More work because these are sketchy

            // First check if we're still in a stuck state from last frame
            if (IsStuck)
            {
                switch (AntiStuckMethod)
                {
                    case AntiStuckMethods.Jiggle:
                        if (!_tryingAntiStuck)
                        {
                            var randomRange = IoCManager.Resolve<IRobustRandom>().Next(0, 359);
                            var angle = Angle.FromDegrees(randomRange);
                            Owner.TryGetComponent(out AiControllerComponent mover);
                            mover.VelocityDir = angle.ToVec().Normalized * Speed;
                            _tryingAntiStuck = true;
                        }

                        if (_antiStuckTryTimer > 0)
                        {
                            break;
                        }

                        _isStuck = false;
                        break;
                    case AntiStuckMethods.PhaseThrough:
                        if (Owner.TryGetComponent(out CollidableComponent collidableComponent))
                        {
                            // TODO Fix this because they are yeeting themselves when they charge
                            if (!_tryingAntiStuck)
                            {
                                // TODO: If something updates this this will fuck it
                                collidableComponent.CollisionEnabled = false;
                                Logger.DebugS("ai", $"{Owner} became stuck, turning off collidable temporarily");
                                _tryingAntiStuck = true;
                            }

                            if (_antiStuckTryTimer > 0)
                            {
                                break;
                            }
                            Logger.DebugS("ai", $"Anti-stuck turning back on collidable for {Owner}");
                            collidableComponent.CollisionEnabled = true;
                            _isStuck = false;
                        }
                        break;
                    case AntiStuckMethods.Teleport:
                        Owner.Transform.DetachParent();
                        Owner.Transform.GridPosition = NextGrid;
                        _isStuck = false;
                        Logger.DebugS("ai", $"Teleported {Owner} to {NextGrid} for anti-stuck");
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }

            _stuckTimerRemaining -= frameTime;
            _antiStuckTryTimer -= frameTime;

            // Stuck check cooldown
            if (_stuckTimerRemaining > 0.0f)
            {
                return;
            }

            _tryingAntiStuck = false;
            _stuckTimerRemaining = 2.0f;
            _antiStuckTryTimer = 0.5f;

            // Are we actually stuck
            if ((OurLastPosition.Position - Owner.Transform.GridPosition.Position).Length < TileTolerance)
            {
                _isStuck = true;
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

        /// <summary>
        /// Will move the AI towards the next position
        /// </summary>
        /// <returns>true if movement to be done</returns>
        protected bool TryMove()
        {
            // Fix getting stuck on corners
            // TODO: Potentially change this. This is just because the position doesn't match the aabb so we need to make sure corners don't fuck us
            Owner.TryGetComponent<ICollidableComponent>(out var collidableComponent);
            var targetDiff = NextGrid.Position - collidableComponent.WorldAABB.Center;
            // Check distance
            if (targetDiff.Length > TileTolerance)
            {
                // Move towards it
                Owner.TryGetComponent(out AiControllerComponent mover);
                mover.VelocityDir = targetDiff.Normalized * Speed;
                return true;
            }

            return false;
        }

        public virtual void Update(float frameTime)
        {
            HandleMovement(frameTime);
        }
    }

    public enum AntiStuckMethods
    {
        Jiggle, // Just pick a random direction for a bit and hope for the best
        Teleport, // The Half-Life 2 method
        PhaseThrough, // Just makes it non-collidable
    }
}
