using System.Linq;
using System.Numerics;
using Content.Client.Radiation.Systems;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Client.Radiation.Overlays;

public sealed partial class RadiationDebugOverlay : Overlay
{
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IResourceCache _cache = default!;

    private readonly SharedMapSystem _mapSystem;
    private readonly RadiationSystem _radiation;

    private readonly Font _font;

    public override OverlaySpace Space => OverlaySpace.WorldSpace | OverlaySpace.ScreenSpace;

    public RadiationDebugOverlay()
    {
        IoCManager.InjectDependencies(this);
        _radiation = _entityManager.System<RadiationSystem>();
        _mapSystem = _entityManager.System<SharedMapSystem>();

        _font = new VectorFont(_cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 8);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        switch (args.Space)
        {
            case OverlaySpace.ScreenSpace:
                DrawScreenRays(args);
                DrawScreenResistance(args);
                break;
            case OverlaySpace.WorldSpace:
                DrawWorld(args);
                break;
        }
    }

    private void DrawScreenRays(OverlayDrawArgs args)
    {
        var rays = _radiation.Rays;
        if (rays == null || args.ViewportControl == null)
            return;

        var handle = args.ScreenHandle;
        foreach (var ray in rays)
        {
            var rayMap = _mapSystem.GetMapOrInvalid(ray.MapId);
            if (!args.VisibleMaps.Contains(rayMap))
                continue;

            if (ray.ReachedDestination)
            {
                if (!args.TryProjectMapCoordinates(new MapCoordinates(ray.Destination, ray.MapId), out var destination))
                    continue;

                var screenCenter = args.ViewportControl.WorldToScreen(destination);
                handle.DrawString(_font, screenCenter, ray.Rads.ToString("F2"), 2f, Color.White);
            }

            foreach (var (netGrid, blockers) in ray.Blockers)
            {
                var gridUid = _entityManager.GetEntity(netGrid);

                if (!_entityManager.TryGetComponent<MapGridComponent>(gridUid, out var grid))
                    continue;

                if (!args.TryGetEntityPresentedViewMatrix(gridUid, out var gridMatrix, out var opacity))
                    continue;

                var color = Color.White.WithAlpha(opacity);

                foreach (var (tile, rads) in blockers)
                {
                    var localPos = _mapSystem.GridTileToLocal(gridUid, grid, tile).Position;
                    var worldPos = Vector2.Transform(localPos, gridMatrix);
                    var screenCenter = args.ViewportControl.WorldToScreen(worldPos);
                    handle.DrawString(_font, screenCenter, rads.ToString("F2"), 1.5f, color);
                }
            }
        }
    }

    private void DrawScreenResistance(OverlayDrawArgs args)
    {
        var resistance = _radiation.ResistanceGrids;
        if (resistance == null || args.ViewportControl == null)
            return;

        var handle = args.ScreenHandle;
        foreach (var (netGrid, resMap) in resistance)
        {
            var gridUid = _entityManager.GetEntity(netGrid);

            if (!_entityManager.TryGetComponent<MapGridComponent>(gridUid, out var grid))
                continue;
            if (!args.TryGetEntityPresentedViewMatrix(gridUid, out var gridMatrix, out var opacity))
                continue;

            var offset = new Vector2(grid.TileSize, -grid.TileSize) * 0.25f;
            var color = Color.White.WithAlpha(opacity);
            foreach (var (tile, value) in resMap)
            {
                var localPos = _mapSystem.GridTileToLocal(gridUid, grid, tile).Position + offset;
                var worldPos = Vector2.Transform(localPos, gridMatrix);
                var screenCenter = args.ViewportControl.WorldToScreen(worldPos);
                handle.DrawString(_font, screenCenter, value.ToString("F2"), color: color);
            }
        }
    }

    private void DrawWorld(in OverlayDrawArgs args)
    {
        var rays = _radiation.Rays;
        if (rays == null)
            return;

        var handle = args.WorldHandle;
        // draw lines for raycasts
        foreach (var ray in rays)
        {
            if (ray.MapId != args.MapId)
                continue;

            if (ray.ReachedDestination)
            {
                handle.DrawLine(ray.Source, ray.Destination, Color.Red);
                continue;
            }

            foreach (var (netGrid, blockers) in ray.Blockers)
            {
                var gridUid = _entityManager.GetEntity(netGrid);

                if (!_entityManager.TryGetComponent<MapGridComponent>(gridUid, out var grid))
                    continue;
                if (!args.TryGetEntityRenderMatrix(gridUid, out var gridMatrix, out var opacity))
                    continue;
                var (destTile, _) = blockers.Last();
                var destLocal = _mapSystem.GridTileToLocal(gridUid, grid, destTile).Position;
                var destWorld = Vector2.Transform(destLocal, gridMatrix);
                handle.DrawLine(ray.Source, destWorld, Color.Red.WithAlpha(opacity));
            }
        }
    }
}
