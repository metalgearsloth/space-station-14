using System;
using System.Collections.Generic;
using Content.Server.AI.HTN.Blackboard;
using Content.Server.GameObjects.Components.Movement;
using Content.Server.GameObjects.Components.Pathfinding;
using Robust.Server.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.GameObjects.Components;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.Interfaces.Random;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Server.AI.HTN.Tasks.Primitive.Operators.Movement
{
    public abstract class BaseMover : IOperator
    {

        protected Queue<TileRef> Route = new Queue<TileRef>();
        protected GridCoordinates TargetGrid;
        protected GridCoordinates NextGrid;
        private const float TileTolerance = 0.2f;

        // Stuck checkers
        private float _stuckTimerRemaining = 2.0f;
        private GridCoordinates _ourLastPosition;
        private float _antiStuckTryTimer = 0.2f;
        // Anti-stuck measures. See the AntiStuck() method for more details
        private bool _tryingAntiStuck = false;
        public bool IsStuck;
        private AntiStuckMethod _antiStuckMethod = AntiStuckMethod.PhaseThrough;

        // Instance variables
        private IMapManager _mapManager;
        private IPathfinder _pathfinder;
        private ICollidableComponent _ownerCollidable;

        // Input
        protected IEntity Owner;


        public virtual void Setup(IEntity owner)
        {
            Owner = owner;
            _mapManager = IoCManager.Resolve<IMapManager>();
            _pathfinder = IoCManager.Resolve<IPathfinder>();
            if (!Owner.TryGetComponent<ICollidableComponent>(out var collidableComponent))
            {
                throw new InvalidOperationException();
            }

            _ownerCollidable = collidableComponent;
        }

        /// <summary>
        /// Will move the AI towards the next position
        /// </summary>
        /// <returns>true if movement to be done</returns>
        protected bool TryMove()
        {
            // Use collidable just so we don't get stuck on corners as much
            var targetDiff = NextGrid.Position - _ownerCollidable.WorldAABB.Center;

            // Check distance
            if (targetDiff.Length < TileTolerance)
            {
                return false;
            }
            // Move towards it
            Owner.TryGetComponent(out AiControllerComponent mover);
            mover.VelocityDir = targetDiff.Normalized;
            return true;

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
                switch (_antiStuckMethod)
                {
                    case AntiStuckMethod.Jiggle:
                        if (!_tryingAntiStuck)
                        {
                            var randomRange = IoCManager.Resolve<IRobustRandom>().Next(0, 359);
                            var angle = Angle.FromDegrees(randomRange);
                            Owner.TryGetComponent(out AiControllerComponent mover);
                            mover.VelocityDir = angle.ToVec().Normalized;
                            _tryingAntiStuck = true;
                        }

                        if (_antiStuckTryTimer > 0)
                        {
                            break;
                        }

                        IsStuck = false;
                        break;
                    case AntiStuckMethod.PhaseThrough:
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
                            IsStuck = false;
                            // Need to clear the tile out to avoid back and forth
                            var nextTile = Route.Dequeue();
                            NextGrid = _mapManager.GetGrid(nextTile.GridIndex).GridTileToLocal(nextTile.GridIndices);
                        }
                        break;
                    case AntiStuckMethod.Teleport:
                        Owner.Transform.DetachParent();
                        Owner.Transform.GridPosition = NextGrid;
                        IsStuck = false;
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
            if ((_ourLastPosition.Position - Owner.Transform.GridPosition.Position).Length < TileTolerance)
            {
                IsStuck = true;
                Logger.DebugS("ai", $"{Owner} is stuck at {Owner.Transform.GridPosition}");
            }

            // TODO: If we've been stuck say 5 seconds re-route?

            _ourLastPosition = Owner.Transform.GridPosition;
        }

        /// <summary>
        /// Tells this we don't need to keep moving and resets everything
        /// </summary>
        public void HaveArrived()
        {
            Route.Clear();
            Owner.TryGetComponent(out AiControllerComponent mover);
            mover.VelocityDir = Vector2.Zero;
        }

        protected void GetRoute()
        {
            Route.Clear();
            var route = _pathfinder.FindPath(Owner.Transform.GridPosition, TargetGrid, 1.5f);

            if (route == null || route.Count <= 1)
            {
                Route = null;
                // Couldn't find a route to target
                return;
            }

            foreach (var tile in route)
            {
                Route.Enqueue(tile);
            }

            // Because the entity may be half on 2 tiles we'll just cut out the first tile.
            // This may not be the best solution but sometimes if the AI is chasing for example it will
            // stutter backwards to the first tile again.
            Route.Dequeue();

            var nextTile = Route.Peek();
            NextGrid = _mapManager.GetGrid(nextTile.GridIndex).GridTileToLocal(nextTile.GridIndices);
        }

        public abstract Outcome Execute(float frameTime);
    }

    public enum AntiStuckMethod
    {
        Jiggle, // Just pick a random direction for a bit and hope for the best
        Teleport, // The Half-Life 2 method
        PhaseThrough, // Just makes it non-collidable
    }
}
