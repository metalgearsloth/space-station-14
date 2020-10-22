#nullable enable
using System;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Components;
using Robust.Shared.Interfaces.Configuration;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.Interfaces.Timing;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.Shared.ViewVariables;

namespace Content.Shared.GameObjects.Components.Movement
{
    public abstract class SharedShuttleControllerComponent : Component
    {
        [Dependency] private readonly IConfigurationManager _configurationManager = default!;
        [Dependency] private readonly IGameTiming _gameTiming = default!;

        public override string Name => "ShuttleController";
        [Dependency] private readonly IMapManager _mapManager = default!;
        [Dependency] private readonly IEntityManager _entityManager = default!;

        private GameTick _lastInputTick;
        private ushort _lastInputSubTick;
        private Vector2 _curTickWalkMovement;
        private Vector2 _curTickSprintMovement;

        private SharedPlayerInputMoverComponent.MoveButtons _heldMoveButtons = SharedPlayerInputMoverComponent.MoveButtons.None;

        [ViewVariables(VVAccess.ReadWrite)]
        public float CurrentWalkSpeed { get; } = 8;
        public float CurrentSprintSpeed => 0;

        /// <inheritdoc />
        [ViewVariables]
        public float CurrentPushSpeed => 0.0f;

        /// <inheritdoc />
        [ViewVariables]
        public float GrabRange => 0.0f;

        public bool Sprinting => false;

        /// <summary>
        ///     Calculated linear velocity direction of the entity.
        /// </summary>
        [ViewVariables]
        public (Vector2 walking, Vector2 sprinting) VelocityDir
        {
            get
            {
                if (!_gameTiming.InSimulation)
                {
                    // Outside of simulation we'll be running client predicted movement per-frame.
                    // So return a full-length vector as if it's a full tick.
                    // Physics system will have the correct time step anyways.
                    var immediateDir = DirVecForButtons(_heldMoveButtons);
                    return Sprinting ? (Vector2.Zero, immediateDir) : (immediateDir, Vector2.Zero);
                }

                Vector2 walk;
                Vector2 sprint;
                float remainingFraction;

                if (_gameTiming.CurTick > _lastInputTick)
                {
                    walk = Vector2.Zero;
                    sprint = Vector2.Zero;
                    remainingFraction = 1;
                }
                else
                {
                    walk = _curTickWalkMovement;
                    sprint = _curTickSprintMovement;
                    remainingFraction = (ushort.MaxValue - _lastInputSubTick) / (float) ushort.MaxValue;
                }

                var curDir = DirVecForButtons(_heldMoveButtons) * remainingFraction;

                if (Sprinting)
                {
                    sprint += curDir;
                }
                else
                {
                    walk += curDir;
                }

                // Logger.Info($"{curDir}{walk}{sprint}");
                return (walk, sprint);
            }
        }

        public EntityCoordinates LastPosition { get; set; }
        public float StepSoundDistance { get; set; }

        /// <summary>
        ///     Whether or not the player can move diagonally.
        /// </summary>
        [ViewVariables]
        public bool DiagonalMovementEnabled => _configurationManager.GetCVar<bool>("game.diagonalmovement");

        public void SetVelocityDirection(Direction direction, ushort subTick, bool enabled)
        {
            var gridId = Owner.Transform.GridID;

            if (_mapManager.TryGetGrid(gridId, out var grid) &&
                _entityManager.TryGetEntity(grid.GridEntityId, out var gridEntity))
            {
                //TODO: Switch to shuttle component
                if (!gridEntity.TryGetComponent(out IPhysicsComponent? physics))
                {
                    physics = gridEntity.AddComponent<PhysicsComponent>();
                    physics.Mass = 1;
                    physics.CanCollide = true;
                    physics.PhysicsShapes.Add(new PhysShapeGrid(grid));
                }

                var bit = direction switch
                {
                    Direction.East => SharedPlayerInputMoverComponent.MoveButtons.Right,
                    Direction.North => SharedPlayerInputMoverComponent.MoveButtons.Up,
                    Direction.West => SharedPlayerInputMoverComponent.MoveButtons.Left,
                    Direction.South => SharedPlayerInputMoverComponent.MoveButtons.Down,
                    _ => throw new ArgumentException(nameof(direction))
                };

                SetMoveInput(subTick, enabled, bit);
            }
        }

        private void SetMoveInput(ushort subTick, bool enabled, SharedPlayerInputMoverComponent.MoveButtons bit)
        {
            // Modifies held state of a movement button at a certain sub tick and updates current tick movement vectors.

            if (_gameTiming.CurTick > _lastInputTick)
            {
                _curTickWalkMovement = Vector2.Zero;
                _curTickSprintMovement = Vector2.Zero;
                _lastInputTick = _gameTiming.CurTick;
                _lastInputSubTick = 0;
            }

            if (subTick >= _lastInputSubTick)
            {
                var fraction = (subTick - _lastInputSubTick) / (float) ushort.MaxValue;

                ref var lastMoveAmount = ref Sprinting ? ref _curTickSprintMovement : ref _curTickWalkMovement;

                lastMoveAmount += DirVecForButtons(_heldMoveButtons) * fraction;

                _lastInputSubTick = subTick;
            }

            if (enabled)
            {
                _heldMoveButtons |= bit;
            }
            else
            {
                _heldMoveButtons &= ~bit;
            }

            Dirty();
        }

        /// <summary>
        ///     Retrieves the normalized direction vector for a specified combination of movement keys.
        /// </summary>
        private Vector2 DirVecForButtons(SharedPlayerInputMoverComponent.MoveButtons buttons)
        {
            // key directions are in screen coordinates
            // _moveDir is in world coordinates
            // if the camera is moved, this needs to be changed

            var x = 0;
            x -= HasFlag(buttons, SharedPlayerInputMoverComponent.MoveButtons.Left) ? 1 : 0;
            x += HasFlag(buttons, SharedPlayerInputMoverComponent.MoveButtons.Right) ? 1 : 0;

            var y = 0;
            if (DiagonalMovementEnabled || x == 0)
            {
                y -= HasFlag(buttons, SharedPlayerInputMoverComponent.MoveButtons.Down) ? 1 : 0;
                y += HasFlag(buttons, SharedPlayerInputMoverComponent.MoveButtons.Up) ? 1 : 0;
            }

            var vec = new Vector2(x, y);

            // can't normalize zero length vector
            if (vec.LengthSquared > 1.0e-6)
            {
                // Normalize so that diagonals aren't faster or something.
                vec = vec.Normalized;
            }

            return vec;
        }

        private static bool HasFlag(SharedPlayerInputMoverComponent.MoveButtons buttons, SharedPlayerInputMoverComponent.MoveButtons flag)
        {
            return (buttons & flag) == flag;
        }

        public void SetSprinting(ushort subTick, bool walking)
        {
            // Shuttles can't sprint.
        }
    }
}
