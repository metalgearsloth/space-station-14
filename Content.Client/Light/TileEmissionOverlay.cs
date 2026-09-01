using System.Numerics;
using Content.Shared.Light.Components;
using Content.Client.Graphics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client.Light;

public sealed partial class TileEmissionOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.BeforeLighting;

    [Dependency] private IOverlayManager _overlay = default!;

    private SharedMapSystem _mapSystem;
    private TransformSystem _xformSystem;

    private readonly EntityLookupSystem _lookup;

    private readonly EntityQuery<TransformComponent> _xformQuery;
    private readonly HashSet<Entity<TileEmissionComponent>> _entities = new();

    private List<Entity<MapGridComponent>> _grids = new();

    public const int ContentZIndex = RoofOverlay.ContentZIndex + 1;

    public TileEmissionOverlay(IEntityManager entManager)
    {
        IoCManager.InjectDependencies(this);

        _lookup = entManager.System<EntityLookupSystem>();
        _mapSystem = entManager.System<SharedMapSystem>();
        _xformSystem = entManager.System<TransformSystem>();

        _xformQuery = entManager.GetEntityQuery<TransformComponent>();
        ZIndex = ContentZIndex;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.LayerEye is not { } eye)
            return;

        var mapId = args.MapId;
        var worldHandle = args.WorldHandle;
        var lightoverlay = _overlay.GetOverlay<BeforeLightTargetOverlay>();
        var bounds = lightoverlay.EnlargedBounds;
        var target = lightoverlay.GetCachedForViewport(args.Viewport).EnlargedLightTarget;
        var viewport = args.Viewport;
        var layerMap = args.MapUid;
        args.FindRenderGrids(_mapSystem, ref _grids, enlargement: 1f, approx: true);

        if (_grids.Count == 0)
            return;

        var lightScale = viewport.LightRenderTarget.Size / (Vector2) viewport.Size;
        var scale = viewport.RenderScale / (Vector2.One / lightScale);

        args.WorldHandle.RenderInRenderTarget(target,
        () =>
        {
            var invMatrix = target.GetWorldToLocalMatrix(eye, scale);

            foreach (var grid in _grids)
            {
                if (!_xformSystem.TryGetRenderLayerSample(grid.Owner, layerMap, out var renderLayer))
                    continue;

                var gridMatrix = Matrix3Helpers.CreateTransform(renderLayer.Position, renderLayer.Rotation);
                if (!Matrix3x2.Invert(gridMatrix, out var gridInvMatrix))
                {
                    continue;
                }

                var opacity = renderLayer.Opacity;

                var localBounds = gridInvMatrix.TransformBox(bounds);
                _entities.Clear();
                _lookup.GetLocalEntitiesIntersecting(grid.Owner, localBounds, _entities);

                if (_entities.Count == 0)
                    continue;

                foreach (var ent in _entities)
                {
                    var xform = _xformQuery.Comp(ent);

                    var tile = _mapSystem.LocalToTile(grid.Owner, grid, xform.Coordinates);
                    var matty = Matrix3x2.Multiply(gridMatrix, invMatrix);

                    worldHandle.SetTransform(matty);

                    // Yes I am fully aware this leads to overlap. If you really want to have alpha then you'll need
                    // to turn the squares into polys.
                    // Additionally no shadows so if you make it too big it's going to go through a 1x wall.
                    var local = _lookup.GetLocalBounds(tile, grid.Comp.TileSize).Enlarged(ent.Comp.Range);
                    worldHandle.DrawRect(local, ent.Comp.Color.WithAlpha(ent.Comp.Color.A * opacity));
                }
            }
        }, null);
    }
}
