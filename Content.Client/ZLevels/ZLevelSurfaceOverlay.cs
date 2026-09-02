using System.Numerics;
using Content.Client.Interactable.Components;
using Content.Shared.ZLevels;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;

namespace Content.Client.ZLevels;

/// <summary>
/// Draws the real support polygon at its projected absolute height. This is deliberately content-owned: wall art
/// can opt into an unmistakable walkable top without making every engine high-ground provider look the same.
/// </summary>
public sealed class ZLevelSurfaceOverlay : Overlay
{
    private const float NearbyRadius = 1.75f;
    private static readonly Color FillColor = new(160, 160, 160, 24);
    private static readonly Color EdgeColor = new(160, 160, 160, 100);

    private readonly EntityLookupSystem _lookup;
    private readonly IPlayerManager _players;
    private readonly ZLevelSurfaceProjectionSystem _projection;
    private readonly SharedTransformSystem _transform;
    private readonly ZLevelSystem _zLevels;
    private readonly EntityQuery<MapComponent> _mapQuery;
    private readonly EntityQuery<TransformComponent> _xformQuery;
    private readonly EntityQuery<ZLevelHighGroundComponent> _highGroundQuery;
    private readonly EntityQuery<InteractionOutlineComponent> _outlineQuery;
    private readonly HashSet<Entity<ZLevelTopSurfaceVisualComponent>> _surfaces = new();
    private readonly Vector2[] _projectedSurface = new Vector2[4];

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    public ZLevelSurfaceOverlay(IEntityManager entities, IPlayerManager players)
    {
        _lookup = entities.System<EntityLookupSystem>();
        _players = players;
        _projection = entities.System<ZLevelSurfaceProjectionSystem>();
        _transform = entities.System<SharedTransformSystem>();
        _zLevels = entities.System<ZLevelSystem>();
        _mapQuery = entities.GetEntityQuery<MapComponent>();
        _xformQuery = entities.GetEntityQuery<TransformComponent>();
        _highGroundQuery = entities.GetEntityQuery<ZLevelHighGroundComponent>();
        _outlineQuery = entities.GetEntityQuery<InteractionOutlineComponent>();
        ZIndex = (int) Content.Shared.DrawDepth.DrawDepth.WallTops;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!_zLevels.TryGetMapData(args.MapUid, out _, out _) ||
            _players.LocalSession?.AttachedEntity is not { } controlled ||
            !_xformQuery.TryComp(controlled, out var controlledXform) ||
            controlledXform.MapUid == null)
        {
            return;
        }

        var controlledPosition = _transform.GetWorldPosition(controlledXform);
        foreach (var sourceMap in args.VisibleMaps)
        {
            if (!_mapQuery.TryComp(sourceMap, out _) ||
                !args.TryGetMapRenderBounds(sourceMap, out var sourceMapId, out var sourceBounds))
            {
                continue;
            }

            _surfaces.Clear();
            _lookup.GetEntitiesIntersecting(
                sourceMapId,
                new Box2(controlledPosition - new Vector2(NearbyRadius), controlledPosition + new Vector2(NearbyRadius))
                    .Intersect(sourceBounds.CalcBoundingBox()),
                _surfaces);

            foreach (var surface in _surfaces)
                DrawSurface(args, surface);
        }
    }

    private void DrawSurface(in OverlayDrawArgs args, Entity<ZLevelTopSurfaceVisualComponent> surface)
    {
        if (!_highGroundQuery.TryComp(surface.Owner, out var highGround))
        {
            return;
        }

        var projected = _projectedSurface;
        if (!_projection.TryGetProjectedSurface(
                (surface.Owner, highGround),
                args.MapUid,
                projected,
                out var count,
                out var averageHeight) ||
            count < 3 ||
            !_projection.TryGetSurfaceLayer(surface.Owner, averageHeight, out var targetMap) ||
            targetMap != args.MapUid)
        {
            return;
        }

        var handle = args.WorldHandle;
        var fillColor = FillColor;
        var edgeColor = EdgeColor;
        if (_outlineQuery.TryComp(surface.Owner, out var outline) && outline.Active)
        {
            edgeColor = outline.InRange ? Color.Lime : Color.Orange;
            fillColor = edgeColor.WithAlpha(0.28f);
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, projected.AsSpan(0, count), fillColor);
        handle.DrawPrimitives(DrawPrimitiveTopology.LineLoop, projected.AsSpan(0, count), edgeColor);
    }
}
