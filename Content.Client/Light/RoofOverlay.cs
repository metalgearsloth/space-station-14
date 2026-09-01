using System.Numerics;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Maps;
using Content.Client.Graphics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Enumerators;
using Robust.Shared.Physics;

namespace Content.Client.Light;

public sealed partial class RoofOverlay : Overlay
{
    private readonly IEntityManager _entManager;
    [Dependency] private IOverlayManager _overlay = default!;

    private readonly EntityLookupSystem _lookup;
    private readonly SharedMapSystem _mapSystem;
    private readonly SharedRoofSystem _roof = default!;
    private readonly TransformSystem _xformSystem;
    private readonly TurfSystem _turf;

    private List<Entity<MapGridComponent>> _grids = new();

    public override OverlaySpace Space => OverlaySpace.BeforeLighting;

    public const int ContentZIndex = BeforeLightTargetOverlay.ContentZIndex + 1;

    public RoofOverlay(IEntityManager entManager)
    {
        _entManager = entManager;
        IoCManager.InjectDependencies(this);

        _lookup = _entManager.System<EntityLookupSystem>();
        _mapSystem = _entManager.System<SharedMapSystem>();
        _roof = _entManager.System<SharedRoofSystem>();
        _xformSystem = _entManager.System<TransformSystem>();
        _turf = _entManager.System<TurfSystem>();

        ZIndex = ContentZIndex;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.LayerEye == null || !_entManager.HasComponent<MapLightComponent>(args.MapUid))
            return;

        var viewport = args.Viewport;
        var eye = args.LayerEye;

        var worldHandle = args.WorldHandle;
        var lightoverlay = _overlay.GetOverlay<BeforeLightTargetOverlay>();
        var lightRes = lightoverlay.GetCachedForViewport(args.Viewport);
        var bounds = lightoverlay.EnlargedBounds;
        var target = lightRes.EnlargedLightTarget;
        var layerMap = args.MapUid;

        args.FindRenderGrids(_mapSystem, ref _grids, enlargement: 1f, approx: true, includeMap: true);
        var lightScale = viewport.LightRenderTarget.Size / (Vector2) viewport.Size;
        var scale = viewport.RenderScale / (Vector2.One / lightScale);

        worldHandle.RenderInRenderTarget(target,
            () =>
            {
                var invMatrix = target.GetWorldToLocalMatrix(eye, scale);

                for (var i = 0; i < _grids.Count; i++)
                {
                    var grid = _grids[i];

                    if (!_entManager.TryGetComponent(grid.Owner, out ImplicitRoofComponent? roof))
                        continue;

                    if (!_xformSystem.TryGetRenderLayerSample(grid.Owner, layerMap, out var renderLayer))
                        continue;

                    var gridMatrix = Matrix3Helpers.CreateTransform(renderLayer.Position, renderLayer.Rotation);
                    var gridInvMatrix = Matrix3Helpers.CreateInverseTransform(renderLayer.Position, renderLayer.Rotation);
                    var opacity = renderLayer.Opacity;
                    var matty = Matrix3x2.Multiply(gridMatrix, invMatrix);

                    worldHandle.SetTransform(matty);

                    var tileEnumerator = _mapSystem.GetLocalTilesIntersecting(
                        grid.Owner,
                        grid,
                        gridInvMatrix.TransformBox(bounds));
                    var color = roof.Color.WithAlpha(roof.Color.A * opacity);

                    while (tileEnumerator.MoveNext(out var tileRef))
                    {
                        if (_turf.IsSpace(tileRef))
                            continue;

                        var local = _lookup.GetLocalBounds(tileRef, grid.Comp.TileSize);
                        worldHandle.DrawRect(local, color);
                    }

                    // Don't need it for the next stage.
                    _grids.RemoveAt(i);
                    i--;
                }
            }, null);

        worldHandle.RenderInRenderTarget(target,
            () =>
            {
                var invMatrix = target.GetWorldToLocalMatrix(eye, scale);

                foreach (var grid in _grids)
                {
                    if (!_entManager.TryGetComponent(grid.Owner, out RoofComponent? roof))
                        continue;

                    if (!_xformSystem.TryGetRenderLayerSample(grid.Owner, layerMap, out var renderLayer))
                        continue;

                    var gridMatrix = Matrix3Helpers.CreateTransform(renderLayer.Position, renderLayer.Rotation);
                    var gridInvMatrix = Matrix3Helpers.CreateInverseTransform(renderLayer.Position, renderLayer.Rotation);
                    var opacity = renderLayer.Opacity;
                    var matty = Matrix3x2.Multiply(gridMatrix, invMatrix);

                    worldHandle.SetTransform(matty);

                    var tileEnumerator = _mapSystem.GetLocalTilesIntersecting(
                        grid.Owner,
                        grid,
                        gridInvMatrix.TransformBox(bounds));
                    var roofEnt = (grid.Owner, grid.Comp, roof);

                    // Due to stencilling we essentially draw on unrooved tiles
                    while (tileEnumerator.MoveNext(out var tileRef))
                    {
                        if (_turf.IsSpace(tileRef))
                            continue;

                        var color = _roof.GetColor(roofEnt, tileRef.GridIndices);

                        if (color == null)
                        {
                            continue;
                        }

                        var local = _lookup.GetLocalBounds(tileRef, grid.Comp.TileSize);
                        worldHandle.DrawRect(local, color.Value.WithAlpha(color.Value.A * opacity));
                    }
                }
            }, null);

        worldHandle.SetTransform(Matrix3x2.Identity);
    }
}
