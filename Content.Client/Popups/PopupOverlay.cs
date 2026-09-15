using System.Numerics;
using Content.Shared.Examine;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client.Popups;

/// <summary>
/// Draws popup text, either in world or on screen.
/// </summary>
public sealed class PopupOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";

    private readonly IConfigurationManager _configManager;
    private readonly IPlayerManager _playerMgr;
    private readonly IUserInterfaceManager _uiManager;
    private readonly PopupSystem _popup;
    private readonly PopupUIController _controller;
    private readonly ExamineSystemShared _examine;
    private readonly TransformSystem _transform;
    private readonly ShaderInstance _shader;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public PopupOverlay(
        IConfigurationManager configManager,
        IPlayerManager playerMgr,
        IPrototypeManager protoManager,
        IUserInterfaceManager uiManager,
        PopupUIController controller,
        ExamineSystemShared examine,
        TransformSystem transform,
        PopupSystem popup)
    {
        _configManager = configManager;
        _playerMgr = playerMgr;
        _uiManager = uiManager;
        _examine = examine;
        _transform = transform;
        _popup = popup;
        _controller = controller;

        _shader = protoManager.Index(UnshadedShader).Instance();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null)
            return;

        args.DrawingHandle.SetTransform(Matrix3x2.Identity);
        args.DrawingHandle.UseShader(_shader);
        var scale = _configManager.GetCVar(CVars.DisplayUIScale);

        if (scale == 0f)
            scale = _uiManager.DefaultUIScale;

        DrawWorld(args.ScreenHandle, args, scale);

        args.DrawingHandle.UseShader(null);
    }

    private void DrawWorld(DrawingHandleScreen worldHandle, OverlayDrawArgs args, float scale)
    {
        if (_popup.WorldLabels.Count == 0 || args.ViewportControl == null)
            return;

        var matrix = args.ViewportControl.GetWorldToScreenMatrix();
        var ourEntity = _playerMgr.LocalEntity;
        var viewPos = new MapCoordinates(args.WorldAABB.Center, args.MapId);
        var ourPos = args.WorldBounds.Center;
        if (ourEntity != null)
        {
            viewPos = _transform.GetRenderMapCoordinates(ourEntity.Value);
            ourPos = viewPos.Position;
        }

        foreach (var popup in _popup.WorldLabels)
        {
            var parentPose = _transform.GetRenderWorldTransform(popup.InitialPos.EntityId);
            var parentPos = _transform.GetRenderMapCoordinates(parentPose);
            var mapPos = new MapCoordinates(
                parentPos.Position + parentPose.Rotation.RotateVec(popup.InitialPos.Position),
                parentPos.MapId);

            if (mapPos.MapId != args.MapId)
                continue;

            var distance = (mapPos.Position - ourPos).Length();

            // Should handle fade here too wyci.
            if (!args.WorldBounds.Contains(mapPos.Position) || !_examine.InRangeUnOccluded(viewPos, mapPos, distance,
                    e => e == popup.InitialPos.EntityId || e == ourEntity))
                continue;

            var pos = Vector2.Transform(mapPos.Position, matrix);
            _controller.DrawPopup(popup, worldHandle, pos, scale);
        }
    }
}
