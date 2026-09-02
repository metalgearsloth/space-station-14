using System.Numerics;
using System.Linq;
using Content.Client.Interactable.Components;
using Content.Shared.ZLevels;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;

namespace Content.Client.ZLevels;

/// <summary>
/// Draws the real support polygon at its projected absolute height. This is deliberately content-owned: wall art
/// can opt into an unmistakable walkable top without making every engine high-ground provider look the same.
/// </summary>
public sealed class ZLevelSurfaceOverlay : Overlay
{
    private const float QueryEnlargement = 2f;

    private readonly EntityLookupSystem _lookup;
    private readonly ZLevelSurfaceProjectionSystem _projection;
    private readonly ZLevelSystem _zLevels;
    private readonly EntityQuery<MapComponent> _mapQuery;
    private readonly EntityQuery<ZLevelHighGroundComponent> _highGroundQuery;
    private readonly EntityQuery<InteractionOutlineComponent> _outlineQuery;
    private readonly HashSet<Entity<ZLevelTopSurfaceVisualComponent>> _surfaces = new();
    private readonly Vector2[] _projectedSurface = new Vector2[4];

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    public ZLevelSurfaceOverlay(IEntityManager entities)
    {
        _lookup = entities.System<EntityLookupSystem>();
        _projection = entities.System<ZLevelSurfaceProjectionSystem>();
        _zLevels = entities.System<ZLevelSystem>();
        _mapQuery = entities.GetEntityQuery<MapComponent>();
        _highGroundQuery = entities.GetEntityQuery<ZLevelHighGroundComponent>();
        _outlineQuery = entities.GetEntityQuery<InteractionOutlineComponent>();
        ZIndex = (int) Content.Shared.DrawDepth.DrawDepth.WallTops;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!_zLevels.TryGetMapData(args.MapUid, out _, out _))
            return;

        foreach (var sourceMap in args.VisibleMaps.OrderBy(uid => uid.Id))
        {
            if (!_mapQuery.TryComp(sourceMap, out _) ||
                !args.TryGetMapRenderBounds(sourceMap, out var sourceMapId, out var sourceBounds))
            {
                continue;
            }

            _surfaces.Clear();
            _lookup.GetEntitiesIntersecting(
                sourceMapId,
                sourceBounds.CalcBoundingBox().Enlarged(QueryEnlargement),
                _surfaces);

            foreach (var surface in _surfaces.OrderBy(entity => entity.Owner.Id))
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
        var fillColor = surface.Comp.FillColor;
        var edgeColor = surface.Comp.EdgeColor;
        if (_outlineQuery.TryComp(surface.Owner, out var outline) && outline.Active)
        {
            edgeColor = outline.InRange ? Color.Lime : Color.Orange;
            fillColor = edgeColor.WithAlpha(0.28f);
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, projected.AsSpan(0, count), fillColor);
        handle.DrawPrimitives(DrawPrimitiveTopology.LineLoop, projected.AsSpan(0, count), edgeColor);
    }
}
