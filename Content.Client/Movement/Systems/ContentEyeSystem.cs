using System.Numerics;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client.Movement.Systems;

public sealed class ContentEyeSystem : SharedContentEyeSystem
{
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public void RequestZoom(EntityUid uid, Vector2 zoom, ContentEyeComponent? content = null)
    {
        if (!Resolve(uid, ref content, false))
            return;

        RaisePredictiveEvent(new RequestTargetZoomEvent()
        {
            TargetZoom = zoom,
        });
    }

    public void RequestToggleFov()
    {
        if (_player.LocalPlayer?.ControlledEntity is { } player)
            RequestToggleFov(player);
    }

    public void RequestToggleFov(EntityUid uid, EyeComponent? eye = null)
    {
        if (Resolve(uid, ref eye, false))
            RequestFov(!eye.DrawFov);
    }

    public void RequestFov(bool value)
    {
        RaisePredictiveEvent(new RequestFovEvent()
        {
            Fov = value,
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var localPlayer = _player.LocalPlayer?.ControlledEntity;

        // Eye updates
        if (TryComp<ContentEyeComponent>(localPlayer, out var content) &&
            TryComp<EyeComponent>(localPlayer, out var eye))
        {
            UpdateEyeZoom(localPlayer.Value, content, eye, frameTime);

            if (!_timing.IsFirstTimePredicted)
                return;

            var mousePos = _inputManager.MouseScreenPosition;
            // TODO: Better Mapid validation
            // TODO: Need better validation on the shit being sent
            var playerPos = _transform.GetWorldPosition(localPlayer.Value);
            var mouseMapPos = _eyeManager.ScreenToMap(mousePos);
            Vector2 mouseRelativePos;

            if (mouseMapPos.MapId != MapId.Nullspace)
            {
                mouseRelativePos = mouseMapPos.Position - playerPos;
                var eyeRotation = _eyeManager.CurrentEye.Rotation;
                mouseRelativePos = mouseRelativePos;
                var modifier = 0.3f;
                mouseRelativePos *= modifier;

                if (!content.TargetPosition.Equals(mouseRelativePos))
                {
                    RaisePredictiveEvent(new RequestTargetPositionEvent() { Position = mouseRelativePos});
                }
            }
        }
    }
}
