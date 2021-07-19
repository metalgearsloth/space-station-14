using System;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client.Camera
{
    /// <summary>
    /// Handles maintaining Eye rotation with the grid / map as relevant.
    /// </summary>
    [UsedImplicitly]
    internal sealed class CameraRotationSystem : EntitySystem
    {
        [Dependency] private readonly IEyeManager _eyeManager = default!;
        [Dependency] private readonly IMapManager _mapManager = default!;
        [Dependency] private readonly IPlayerManager _playerManager = default!;

        // Just for debugging now, though probably better to just make a cvar whenever.
        private bool _enabled = true;

        /// <summary>
        /// The entity our eye rotation is relative to.
        /// </summary>
        private EntityUid? _relativeUid;

        private bool _lerping;

        /// <summary>
        /// If our rotation difference is under this amount we'll just align to the target.
        /// </summary>
        private const double Tolerance = 0.01;

        /// <summary>
        /// Max speed the camera can rotate per second, in radians.
        /// </summary>
        const double MaxChangePerSecond = Math.PI / 4;

        public override void FrameUpdate(float frameTime)
        {
            base.FrameUpdate(frameTime);

            if (!_enabled) return;

            var playerEntity = _playerManager.LocalPlayer?.ControlledEntity;
            var currentMap = _eyeManager.CurrentMap;

            if (playerEntity == null ||
                currentMap == MapId.Nullspace)
            {
                _eyeManager.CurrentEye.Rotation = Angle.Zero;
                _relativeUid = null;
                return;
            }

            var gridId = playerEntity.Transform.GridID;

            UpdateRelative(
                gridId == GridId.Invalid
                ? _mapManager.GetMapEntity(currentMap).Uid
                : _mapManager.GetGrid(gridId).GridEntityId);

            if (_relativeUid == null || !EntityManager.TryGetEntity(_relativeUid.Value, out var relativeEntity))
            {
                ResetRotation();
                return;
            }

            var targetRotation = -relativeEntity.Transform.WorldRotation;

            // To avoid disorientation on changes we'll just lerp the eye's rotation to what we want.
            if (_lerping)
            {
                // In case one thing has a billion rotation (unless we get clamping for rotations in at some stage).
                var eyeClampedRotation = _eyeManager.CurrentEye.Rotation % MathF.Tau;
                var targetClampedRotation = targetRotation % MathF.Tau;

                var difference = (eyeClampedRotation - targetClampedRotation);

                if (Math.Abs(difference) < Tolerance)
                {
                    _lerping = false;
                }
                else
                {
                    UpdateEyeRotation(frameTime, difference);
                    return;
                }
            }

            _eyeManager.CurrentEye.Rotation = targetRotation;
        }

        /// <summary>
        /// Rotate our CurrentEye closer to the target rotation.
        /// </summary>
        private void UpdateEyeRotation(float frameTime, double difference)
        {
            var frameChange = MaxChangePerSecond * frameTime;

            // TODO: Need to play around with different curves for something that feels nice.
            var change = Math.Clamp(difference, -frameChange, frameChange);

            _eyeManager.CurrentEye.Rotation -= change;
        }

        private void ResetRotation()
        {
            _eyeManager.CurrentEye.Rotation = Angle.Zero;
            _lerping = false;
        }

        private void UpdateRelative(EntityUid uid)
        {
            if (uid == _relativeUid)
            {
                return;
            }

            _lerping = true;
            _relativeUid = uid;
        }
    }
}
