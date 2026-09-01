using Content.Client.Weapons.Ranged.Systems;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client.Weapons.Ranged;

public sealed class GunSpreadOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private IEntityManager _entManager;
    private readonly IEyeManager _eye;
    private readonly IGameTiming _timing;
    private readonly IInputManager _input;
    private readonly IPlayerManager _player;
    private readonly GunSystem _guns;
    private readonly SharedTransformSystem _transform;

    public GunSpreadOverlay(IEntityManager entManager, IEyeManager eyeManager, IGameTiming timing, IInputManager input, IPlayerManager player, GunSystem system, SharedTransformSystem transform)
    {
        _entManager = entManager;
        _eye = eyeManager;
        _input = input;
        _timing = timing;
        _player = player;
        _guns = system;
        _transform = transform;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var worldHandle = args.WorldHandle;

        var player = _player.LocalEntity;

        if (player == null ||
            !_entManager.TryGetComponent<TransformComponent>(player, out var xform))
        {
            return;
        }

        if (!_guns.TryGetGun(player.Value, out var gun))
            return;

        var mouseScreenPos = _input.MouseScreenPosition;
        var mousePos = _eye.PixelToMap(mouseScreenPos);

        if (!args.TryGetEntityRenderLayer(player.Value, out var renderLayer) ||
            !args.TryProjectMapCoordinates(mousePos, out var projectedMouse))
            return;

        // (☞ﾟヮﾟ)☞
        var maxSpread = gun.Comp.MaxAngleModified;
        var minSpread = gun.Comp.MinAngleModified;
        var timeSinceLastFire = (_timing.CurTime - gun.Comp.NextFire).TotalSeconds;
        var currentAngle = new Angle(MathHelper.Clamp(gun.Comp.CurrentAngle.Theta - gun.Comp.AngleDecayModified.Theta * timeSinceLastFire,
            gun.Comp.MinAngleModified.Theta, gun.Comp.MaxAngleModified.Theta));
        var mapPos = renderLayer.Position;
        var direction = projectedMouse - mapPos;
        var opacity = renderLayer.Opacity;

        worldHandle.DrawLine(mapPos, projectedMouse + direction, Color.Orange.WithAlpha(opacity));

        // Show max spread either side
        worldHandle.DrawLine(mapPos, projectedMouse + maxSpread.RotateVec(direction), Color.Red.WithAlpha(opacity));
        worldHandle.DrawLine(mapPos, projectedMouse + (-maxSpread).RotateVec(direction), Color.Red.WithAlpha(opacity));

        // Show min spread either side
        worldHandle.DrawLine(mapPos, projectedMouse + minSpread.RotateVec(direction), Color.Green.WithAlpha(opacity));
        worldHandle.DrawLine(mapPos, projectedMouse + (-minSpread).RotateVec(direction), Color.Green.WithAlpha(opacity));

        // Show current angle
        worldHandle.DrawLine(mapPos, projectedMouse + currentAngle.RotateVec(direction), Color.Yellow.WithAlpha(opacity));
        worldHandle.DrawLine(mapPos, projectedMouse + (-currentAngle).RotateVec(direction), Color.Yellow.WithAlpha(opacity));
    }
}
