using System;
using System.Collections.Generic;
using Content.Server.GameObjects.Components.Movement;
using Content.Server.GameObjects.Components.Pathfinding;
using Content.Server.GameObjects.EntitySystems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.GameObjects.Components;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.Interfaces.Random;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Server.AI.Routines
{
    public class MoveToGridCoordsAiRoutine : AiRoutine
    {
        // Route handler
        public IReadOnlyCollection<TileRef> Route => _route;
        private Queue<TileRef> _route = new Queue<TileRef>();
        private GridCoordinates _nextGrid;
        private bool _availableRoute = true;
        private DateTime _entityRouteThrottle = DateTime.Now - TimeSpan.FromSeconds(5.0f);

        public bool MovementAllowed { get; set; }

        public override bool RequiresMover => false;

        // Stuck checkers
        private DateTime _lastStuckCheck = DateTime.Now;
        private GridCoordinates _ourLastPosition = default;

        public bool Arrived => _arrived;
        private bool _arrived = true;

        private IEntity _owner;

        [Dependency] private IMapManager _mapManager;
        [Dependency] private IPathfinder _pathfinder;

        private GridCoordinates _targetPosition;
        // How close we need to get
        public float TargetTolerance
        {
            get;
            set;
        }
        // How close to the route X / Y (centre of tile, at least currently) before good enough.
        private float _tileTolerance = 0.1f;

        /// <summary>
        /// Called once when the routine is being setup
        /// </summary>
        /// <param name="owner"></param>
        /// <exception cref="InvalidOperationException"></exception>
        public override void Setup(IEntity owner)
        {
            base.Setup(owner);
            IoCManager.InjectDependencies(this);
            MovementAllowed = true;
            if (!owner.HasComponent<AiControllerComponent>())
            {
                Logger.FatalS("ai_routine", "MovementAiRoutine must have a valid controller on the owner");
                throw new InvalidOperationException();
            }
            _owner = owner;
            TargetTolerance = 2.0f;
        }

        /// <summary>
        /// Will try and wiggle around if it seems like we're stuck
        /// </summary>
        private void AntiStuck()
        {
            if (!((DateTime.Now - _lastStuckCheck).TotalSeconds > 5.0f))
            {
                return;
            }

            _lastStuckCheck = DateTime.Now;

            // Are we actually stuck
            if ((_ourLastPosition.Position - _owner.Transform.GridPosition.Position).Length < TargetTolerance)
            {
                // Move in a random direction
                _owner.TryGetComponent(out AiControllerComponent mover);
                var robustRandom = IoCManager.Resolve<IRobustRandom>();
                var angle = Angle.FromDegrees(robustRandom.Next(359));
                mover.VelocityDir = angle.ToVec().Normalized;
            }
        }

        /// <summary>
        /// Gets a route to the specified target grid
        /// </summary>
        /// <param name="gridCoordinates"></param>
        public void GetRoute(GridCoordinates gridCoordinates)
        {
            ClearRoute();
            _arrived = false;

            foreach (var tile in _pathfinder.FindPath(_owner.Transform.GridPosition, gridCoordinates))
            {
                _route.Enqueue(tile);
            }

            var nextTile = _route.Dequeue();
            _nextGrid = _mapManager.GetGrid(nextTile.GridIndex).GridTileToLocal(nextTile.GridIndices);
            _targetPosition = gridCoordinates;
        }

        /// <summary>
        /// Tells this routine we don't need to keep moving
        /// </summary>
        public void ClearRoute()
        {
            _route.Clear();
        }

        /// <summary>
        /// Will move the owner to the next tile until close enough, then proceed to next tile.
        /// If it seems like we're stuck will move to a random close spot and keep trying to push on.
        /// </summary>
        public void HandleMovement()
        {
            Vector2 targetDiff;
            if (!MovementAllowed ||
                Arrived ||
                _mapManager.GetGrid(_owner.Transform.GridID).GetTileRef(_owner.Transform.GridPosition).Tile.IsEmpty)
            {
                return;
            }

            if ((_owner.Transform.GridPosition.Position - _targetPosition.Position).Length <= TargetTolerance)
            {
                _arrived = true;
                return;
            }

            AntiStuck();

            // Fix getting stuck on corners
            // TODO: Potentially change this. This is just because the position doesn't match the aabb so we need to make sure corners don't fuck us
            _owner.TryGetComponent<ICollidableComponent>(out var collidableComponent);
            targetDiff = _nextGrid.Position - collidableComponent.WorldAABB.Center;
            // Check distance
            if (targetDiff.Length > _tileTolerance)
            {
                // Move towards it
                _owner.TryGetComponent(out AiControllerComponent mover);
                mover.VelocityDir = targetDiff.Normalized;
                return;
            }

            // Have we arrived at the final destination
            if (_route.Count == 0)
            {
                _owner.TryGetComponent(out AiControllerComponent mover);
                mover.VelocityDir = Vector2.Zero;
                _arrived = true;
                return;
            }
            var nextTile = _route.Dequeue();
            _nextGrid = _mapManager.GetGrid(nextTile.GridIndex).GridTileToLocal(nextTile.GridIndices);
        }

        public override void Update()
        {
            base.Update();
            HandleMovement();
        }
    }
}
