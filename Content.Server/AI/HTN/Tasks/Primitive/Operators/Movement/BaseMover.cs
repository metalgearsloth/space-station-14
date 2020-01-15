using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.GameObjects.Components.Movement;
using Content.Server.GameObjects.EntitySystems.Pathfinding;
using Content.Server.GameObjects.EntitySystems.Pathfinding.Pathfinders;
using Robust.Shared.GameObjects.Components;
using Robust.Shared.Interfaces.GameObjects;
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
        public float SpeedMultiplier
        {
            get => _speedMultiplier;
            set
            {
                // Need to cap currently because of physics
                if (value < 0.3f)
                {
                    value = 0.3f;
                } else if (value > 1.0f)
                {
                    value = 1.0f;
                }

                _speedMultiplier = value;
                _controller.SprintMoveSpeed = _speedMultiplier * 7.0f;
            }
        }

        private float _speedMultiplier = 1.0f;
        public float PathfindingProximity { get; set; } = 1.42f;
        protected Queue<TileRef> Route = new Queue<TileRef>();
        protected GridCoordinates TargetGrid;
        protected GridCoordinates NextGrid;
        private const float TileTolerance = 0.2f;

        // Stuck checkers
        private float _stuckTimerRemaining = 1.0f;
        private GridCoordinates _ourLastPosition;
        private float _antiStuckTryTimer = 0.2f;
        // Anti-stuck measures. See the AntiStuck() method for more details
        private bool _tryingAntiStuck = false;
        public bool IsStuck;
        private AntiStuckMethod _antiStuckMethod = AntiStuckMethod.ReRoute;
        public event Action Stuck;

        // Instance variables
        private CancellationTokenSource _routeCancelToken;
        protected Task<Queue<TileRef>> RouteTask;
        private IMapManager _mapManager;
        private PathfindingSystem _pathfinder;
        private AiControllerComponent _controller;

        // Input
        protected IEntity Owner;

        protected void Setup(IEntity owner)
        {
            Owner = owner;
            _mapManager = IoCManager.Resolve<IMapManager>();
            _pathfinder = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<PathfindingSystem>();
            if (!Owner.TryGetComponent(out AiControllerComponent controllerComponent))
            {
                throw new InvalidOperationException();
            }

            _controller = controllerComponent;
        }

        /// <summary>
        /// Will move the AI towards the next position
        /// </summary>
        /// <returns>true if movement to be done</returns>
        protected bool TryMove()
        {
            // Use collidable just so we don't get stuck on corners as much
            // var targetDiff = NextGrid.Position - _ownerCollidable.WorldAABB.Center;
            var targetDiff = NextGrid.Position - Owner.Transform.GridPosition.Position;

            // Check distance
            if (targetDiff.Length < TileTolerance)
            {
                return false;
            }
            // Move towards it
            if (_controller == null)
            {
                return false;
            }
            _controller.VelocityDir = targetDiff.Normalized;
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
                    case AntiStuckMethod.None:
                        IsStuck = false;
                        break;
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
                    case AntiStuckMethod.ReRoute:
                        GetRoute();
                        IsStuck = false;
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
                Stuck?.Invoke();
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
            _routeCancelToken?.Cancel(); // oh thank god no more pathfinding
            Route.Clear();
            if (_controller == null) return;
            _controller.VelocityDir = Vector2.Zero;
        }

        protected void GetRoute()
        {
            _routeCancelToken?.Cancel();
            _routeCancelToken = new CancellationTokenSource();
            Route.Clear();

            int collisionMask;
            if (!Owner.TryGetComponent(out CollidableComponent collidableComponent))
            {
                collisionMask = 0;
            }
            else
            {
                collisionMask = collidableComponent.CollisionMask;
            }

            var startGrid = _mapManager.GetGrid(Owner.Transform.GridID).GetTileRef(Owner.Transform.GridPosition);
            var endGrid = _mapManager.GetGrid(TargetGrid.GridID).GetTileRef(TargetGrid);;
            _routeCancelToken = new CancellationTokenSource();

            RouteTask = _pathfinder.RequestPathAsync(new PathfindingArgs(
                collisionMask,
                startGrid,
                endGrid,
                PathfindingProximity
            ), _routeCancelToken);
        }

        protected void ReceivedRoute()
        {
            Route = RouteTask.Result;

            if (RouteTask.Status != TaskStatus.RanToCompletion || Route == null)
            {
                RouteTask = null;
                Route = new Queue<TileRef>();
                // Couldn't find a route to target
                return;
            }

            RouteTask = null;

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
        None,
        ReRoute,
        Jiggle, // Just pick a random direction for a bit and hope for the best
        Teleport, // The Half-Life 2 method
        PhaseThrough, // Just makes it non-collidable
    }
}
