using System.Numerics;
using Content.Client.Parallax.Managers;
using Content.Shared.CCVar;
using Content.Shared.Parallax.Biomes;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Parallax;

public sealed partial class ParallaxOverlay : Overlay
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IConfigurationManager _configurationManager = default!;
    [Dependency] private IParallaxManager _manager = default!;
    private readonly ParallaxSystem _parallax;
    private readonly ClientZLevelSystem _clientZLevels;
    private readonly ZLevelSystem _zLevels;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld;

    public ParallaxOverlay()
    {
        ZIndex = ParallaxSystem.ParallaxZIndex;
        IoCManager.InjectDependencies(this);
        _parallax = _entManager.System<ParallaxSystem>();
        _clientZLevels = _entManager.System<ClientZLevelSystem>();
        _zLevels = _entManager.System<ZLevelSystem>();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!TryGetViewedMap(args, out var mapUid, out _, out _) ||
            _entManager.HasComponent<BiomeComponent>(mapUid))
            return false;

        return IsBackgroundLayer(args);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!TryGetViewedMap(args, out _, out var mapId, out var backgroundOffset))
            return;

        if (!_configurationManager.GetCVar(CCVars.ParallaxEnabled))
            return;

        var position = args.Viewport.Eye?.Position.Position ?? Vector2.Zero;
        var worldHandle = args.WorldHandle;
        var viewBounds = args.WorldAABB.Translated(-backgroundOffset);
        worldHandle.SetTransform(Matrix3x2.CreateTranslation(backgroundOffset));

        var layers = _parallax.GetParallaxLayers(mapId);
        var realTime = (float) _timing.RealTime.TotalSeconds;

        foreach (var layer in layers)
        {
            ShaderInstance? shader;

            if (!string.IsNullOrEmpty(layer.Config.Shader))
                shader = _prototypeManager.Index<ShaderPrototype>(layer.Config.Shader).Instance();
            else
                shader = null;

            worldHandle.UseShader(shader);
            var tex = layer.Texture;

            // Size of the texture in world units.
            var size = (tex.Size / (float) EyeManager.PixelsPerMeter) * layer.Config.Scale;

            // The "home" position is the effective origin of this layer.
            // Parallax shifting is relative to the home, and shifts away from the home and towards the Eye centre.
            // The effects of this are such that a slowness of 1 anchors the layer to the centre of the screen, while a slowness of 0 anchors the layer to the world.
            // (For values 0.0 to 1.0 this is in effect a lerp, but it's deliberately unclamped.)
            // The ParallaxAnchor adapts the parallax for station positioning and possibly map-specific tweaks.
            var home = layer.Config.WorldHomePosition + _manager.ParallaxAnchor;
            var scrolled = layer.Config.Scrolling * realTime;

            // Origin - start with the parallax shift itself.
            var originBL = (position - home) * layer.Config.Slowness + scrolled;

            // Place at the home.
            originBL += home;

            // Adjust.
            originBL += layer.Config.WorldAdjustPosition;

            // Centre the image.
            originBL -= size / 2;

            if (layer.Config.Tiled)
            {
                // Remove offset so we can floor.
                var flooredBL = viewBounds.BottomLeft - originBL;

                // Floor to background size.
                flooredBL = (flooredBL / size).Floored() * size;

                // Re-offset.
                flooredBL += originBL;

                for (var x = flooredBL.X; x < viewBounds.Right; x += size.X)
                {
                    for (var y = flooredBL.Y; y < viewBounds.Top; y += size.Y)
                    {
                        worldHandle.DrawTextureRect(tex, Box2.FromDimensions(new Vector2(x, y), size));
                    }
                }
            }
            else
            {
                worldHandle.DrawTextureRect(tex, Box2.FromDimensions(originBL, size));
            }
        }

        worldHandle.UseShader(null);
        worldHandle.SetTransform(Matrix3x2.Identity);
    }

    private bool TryGetViewedMap(
        in OverlayDrawArgs args,
        out EntityUid mapUid,
        out MapId mapId,
        out Vector2 backgroundOffset)
    {
        mapUid = args.MapUid;
        mapId = args.MapId;
        backgroundOffset = Vector2.Zero;
        if (mapId == MapId.Nullspace)
            return false;

        if (args.ZLevelOffset == 0)
            return true;

        if (!_zLevels.TryGetMapData(args.MapUid, out var layer, out _) ||
            !_zLevels.TryGetMapAtDepth(layer.Network, layer.Depth - args.ZLevelOffset, out var viewedMap) ||
            !_entManager.TryGetComponent(viewedMap.Value, out MapComponent? map))
        {
            return false;
        }

        mapUid = viewedMap.Value;
        mapId = map.MapId;
        backgroundOffset = ZLevelProjection.GetLayerEyeOffset(
            args.ZLevelOffset,
            _clientZLevels.GetVisuals(layer.Network).ProjectionOffset);
        return true;
    }

    private bool IsBackgroundLayer(in OverlayDrawArgs args)
    {
        if (!_zLevels.TryGetMapData(args.MapUid, out var current, out _))
            return true;

        foreach (var map in args.Viewport.VisibleZMaps)
        {
            if (_zLevels.TryGetMapData(map, out var visible, out _) &&
                visible.Network == current.Network &&
                visible.Depth < current.Depth)
            {
                return false;
            }
        }

        return true;
    }
}

