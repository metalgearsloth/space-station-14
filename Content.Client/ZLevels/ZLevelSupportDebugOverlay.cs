using System.Numerics;
using Content.Client.Resources;
using Content.Shared.ZLevels;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Physics.Components;

namespace Content.Client.ZLevels;

/// <summary>
/// Visual half of support diagnostics. The server-side zphysics_debug command supplies the bounded candidate list
/// and rejection reasons; this overlay shows the confirmed contact, projected safe polygon, and replicated
/// reconciliation state at exactly the positions used by presentation and picking.
/// </summary>
public sealed class ZLevelSupportDebugOverlay : Overlay
{
    private const float CrossSize = 0.12f;

    private readonly IEntityManager _entities;
    private readonly ZLevelSystem _zLevels;
    private readonly ZLevelSurfaceProjectionSystem _surfaces;
    private readonly EntityQuery<ZLevelHighGroundComponent> _highGroundQuery;
    private readonly Font _font;
    private readonly HashSet<EntityUid> _drawnProviders = new();
    private readonly Vector2[] _contactLines = new Vector2[6];
    private readonly Vector2[] _projectedSurface = new Vector2[4];

    public override OverlaySpace Space => OverlaySpace.WorldSpace | OverlaySpace.ScreenSpace;

    public ZLevelSupportDebugOverlay(IEntityManager entities, IResourceCache resources)
    {
        _entities = entities;
        _zLevels = entities.System<ZLevelSystem>();
        _surfaces = entities.System<ZLevelSurfaceProjectionSystem>();
        _highGroundQuery = entities.GetEntityQuery<ZLevelHighGroundComponent>();
        _font = resources.GetFont("/Fonts/NotoSans/NotoSans-Regular.ttf", 10);
        ZIndex = 200;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.Space == OverlaySpace.WorldSpace)
            DrawWorld(args);
        else if (args.Space == OverlaySpace.ScreenSpace)
            DrawLabels(args);
    }

    private void DrawWorld(in OverlayDrawArgs args)
    {
        if (!args.IsViewedMap)
            return;

        _drawnProviders.Clear();
        var lines = _contactLines;
        var polygon = _projectedSurface;
        var query = _entities.EntityQueryEnumerator<ZLevelPhysicsComponent>();
        while (query.MoveNext(out var uid, out var physics))
        {
            if (!args.TryGetEntityPresentedView(uid, out var body) ||
                physics.SupportSurface == ZLevelSupportSurface.None ||
                !_zLevels.TryProjectAbsolutePosition(
                    args.ViewedMapUid,
                    physics.SupportPoint,
                    physics.SupportHeight,
                    out var contact))
            {
                continue;
            }

            var color = GetReconciliationColor(physics.ReconciliationState);
            lines[0] = body.Position;
            lines[1] = contact;
            lines[2] = contact - new Vector2(CrossSize, 0f);
            lines[3] = contact + new Vector2(CrossSize, 0f);
            lines[4] = contact - new Vector2(0f, CrossSize);
            lines[5] = contact + new Vector2(0f, CrossSize);
            args.WorldHandle.DrawPrimitives(DrawPrimitiveTopology.LineList, lines, color);

            if (physics.SupportProvider is not { } provider ||
                !_drawnProviders.Add(provider) ||
                !_highGroundQuery.TryComp(provider, out var highGround))
            {
                continue;
            }

            if (_surfaces.TryGetProjectedSurface(
                    (provider, highGround),
                    args.ViewedMapUid,
                    polygon,
                    out var count,
                    out _))
            {
                args.WorldHandle.DrawPrimitives(
                    DrawPrimitiveTopology.LineLoop,
                    polygon.AsSpan(0, count),
                    Color.Yellow.WithAlpha(0.9f));
            }
        }
    }

    private void DrawLabels(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null)
            return;

        var query = _entities.EntityQueryEnumerator<ZLevelPhysicsComponent>();
        while (query.MoveNext(out var uid, out var physics))
        {
            if (!args.TryGetEntityPresentedView(uid, out var body) ||
                !args.WorldBounds.Contains(body.Position))
            {
                continue;
            }

            var screen = args.ViewportControl.WorldToScreen(body.Position) + new Vector2(10f, -24f);
            var label = $"{uid} {physics.GroundState}\n" +
                        $"{physics.SupportSurface} z={physics.SupportHeight:F3} {physics.ReconciliationState}";
            args.ScreenHandle.DrawString(
                _font,
                screen,
                label,
                color: GetReconciliationColor(physics.ReconciliationState));
        }
    }

    private static Color GetReconciliationColor(ZLevelReconciliationState state)
        => state switch
        {
            ZLevelReconciliationState.Confirmed => Color.Lime,
            ZLevelReconciliationState.AuthoritativeLanding => Color.Cyan,
            ZLevelReconciliationState.AuthoritativeFall => Color.Orange,
            ZLevelReconciliationState.AuthoritativeMapTransition => Color.Magenta,
            _ => Color.Yellow,
        };
}
