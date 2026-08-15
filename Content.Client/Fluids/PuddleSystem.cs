using System.Numerics;
using Content.Client.IconSmoothing;
using Content.Shared.Chemistry.Components;
using Content.Shared.CCVar;
using Content.Shared.Fluids;
using Content.Shared.Fluids.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Map;

namespace Content.Client.Fluids;

public sealed partial class PuddleSystem : SharedPuddleSystem
{
    [Dependency] private IconSmoothSystem _smooth = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    [Dependency] private EntityQuery<IconSmoothComponent> _iconSmoothQuery = default!;
    [Dependency] private EntityQuery<MapGridComponent> _gridQuery = default!;
    [Dependency] private EntityQuery<PuddleComponent> _puddleQuery = default!;
    [Dependency] private EntityQuery<SpriteComponent> _spriteQuery = default!;
    [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;

    private static readonly ProtoId<ShaderPrototype> PuddleShader = "Puddle";
    private static readonly Color BaseColor = Color.White;
    private const float BlendRangePixels = 10f;
    private bool _puddleShadersEnabled;
    private readonly Dictionary<EntityUid, ShaderInstance> _puddleShaders = new();

#if DEBUG
    private readonly Dictionary<EntityUid, PuddleShaderDebugState> _shaderDebugState = new();

    public bool TryGetShaderDebugState(EntityUid uid, out PuddleShaderDebugState state)
    {
        return _shaderDebugState.TryGetValue(uid, out state);
    }
#endif

    public override void Initialize()
    {
        base.Initialize();
        InitializeSharedPuddle();

        Subs.CVar(_cfg, CCVars.PuddleShaders, OnPuddleShadersChanged, true);
    }

    protected override void TickEvaporation()
    {
        TickEvaporation(_puddleQuery);
    }

    [SubscribeLocalEvent]
    private void OnPuddleAppearance(Entity<PuddleComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var (uid, component) = ent;
        var volume = 1f;

        if (args.AppearanceData.TryGetValue(PuddleVisuals.CurrentVolume, out var volumeObj))
        {
            volume = (float)volumeObj;
        }

        // Update smoothing and sprite based on volume.
        if (_iconSmoothQuery.TryComp(uid, out var smooth))
        {
            if (volume < LowThreshold)
            {
                _sprite.LayerSetRsiState((uid, args.Sprite), 0, $"{smooth.StateBase}a");
                _smooth.SetEnabled(uid, false, smooth);
            }
            else if (volume < MediumThreshold)
            {
                _sprite.LayerSetRsiState((uid, args.Sprite), 0, $"{smooth.StateBase}b");
                _smooth.SetEnabled(uid, false, smooth);
            }
            else
            {
                if (!smooth.Enabled)
                {
                    _sprite.LayerSetRsiState((uid, args.Sprite), 0, $"{smooth.StateBase}0");
                    _smooth.SetEnabled(uid, true, smooth);
                    _smooth.DirtyNeighbours(uid);
                }
            }
        }

        var oldColor = component.SolutionColor;

        if (args.AppearanceData.TryGetValue(PuddleVisuals.SolutionColor, out var colorObj))
        {
            component.SolutionColor = (Color) colorObj;
        }
        else
        {
            component.SolutionColor = Color.White;
        }

        ApplyPuddleColor(uid, component, args.Sprite);

        if (component.SolutionColor != oldColor)
            UpdateNeighbourShaders(uid);
    }

    [SubscribeLocalEvent]
    private void OnPuddleSmoothChanged(Entity<PuddleComponent> ent, ref IconSmoothChangedEvent args)
    {
        UpdatePuddleShader(ent.Owner);
        UpdateNeighbourShaders(ent.Owner);
    }

    [SubscribeLocalEvent]
    private void OnPuddleShutdown(Entity<PuddleComponent> ent, ref ComponentShutdown args)
    {
        RemovePuddleShader(ent.Owner, ent.Comp);

        UpdateNeighbourShaders(ent.Owner, ent.Owner, ent.Comp);
    }

    private void OnPuddleShadersChanged(bool enabled)
    {
        _puddleShadersEnabled = enabled;

        var query = AllEntityQuery<PuddleComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var puddle, out var sprite))
        {
            ApplyPuddleColor(uid, puddle, sprite);
        }
    }

    private void ApplyPuddleColor(EntityUid uid, PuddleComponent puddle, SpriteComponent sprite)
    {
        if (!_puddleShadersEnabled)
        {
            RemovePuddleShader(uid, puddle, sprite);
            _sprite.SetColor((uid, sprite), GetUnshadedPuddleColor(puddle));
            return;
        }

        _sprite.SetColor((uid, sprite), BaseColor);
        UpdatePuddleShader(uid, sprite);
    }

    private void UpdatePuddleShader(EntityUid uid, SpriteComponent? sprite = null, EntityUid? ignored = null)
    {
        if (!_spriteQuery.Resolve(uid, ref sprite, false) ||
            !_puddleQuery.TryComp(uid, out var puddle))
            return;

        if (!_puddleShadersEnabled)
        {
            RemovePuddleShader(uid, puddle, sprite);
            _sprite.SetColor((uid, sprite), GetUnshadedPuddleColor(puddle));
            return;
        }

        var shader = GetShader(uid, sprite);
        var color = GetPuddleColor(uid);
        UpdatePuddleLocation(uid, puddle);
        var canBlend = CanBlendPuddleColor(uid);
        var northColor = color;
        var southColor = color;
        var eastColor = color;
        var westColor = color;
        var northEastColor = color;
        var northWestColor = color;
        var southEastColor = color;
        var southWestColor = color;
        var hasNorth = canBlend && TryGetNeighbourColor(uid, Direction.North, out northColor, ignored);
        var hasSouth = canBlend && TryGetNeighbourColor(uid, Direction.South, out southColor, ignored);
        var hasEast = canBlend && TryGetNeighbourColor(uid, Direction.East, out eastColor, ignored);
        var hasWest = canBlend && TryGetNeighbourColor(uid, Direction.West, out westColor, ignored);
        var hasNorthEast = canBlend && TryGetNeighbourColor(uid, Direction.NorthEast, out northEastColor, ignored);
        var hasNorthWest = canBlend && TryGetNeighbourColor(uid, Direction.NorthWest, out northWestColor, ignored);
        var hasSouthEast = canBlend && TryGetNeighbourColor(uid, Direction.SouthEast, out southEastColor, ignored);
        var hasSouthWest = canBlend && TryGetNeighbourColor(uid, Direction.SouthWest, out southWestColor, ignored);

        if (!hasNorth)
            northColor = color;
        if (!hasSouth)
            southColor = color;
        if (!hasEast)
            eastColor = color;
        if (!hasWest)
            westColor = color;
        if (!hasNorthEast)
            northEastColor = color;
        if (!hasNorthWest)
            northWestColor = color;
        if (!hasSouthEast)
            southEastColor = color;
        if (!hasSouthWest)
            southWestColor = color;

        shader.SetParameter("color", color);
        shader.SetParameter("northColor", northColor);
        shader.SetParameter("southColor", southColor);
        shader.SetParameter("eastColor", eastColor);
        shader.SetParameter("westColor", westColor);
        shader.SetParameter("northEastColor", northEastColor);
        shader.SetParameter("northWestColor", northWestColor);
        shader.SetParameter("southEastColor", southEastColor);
        shader.SetParameter("southWestColor", southWestColor);
        shader.SetParameter("cardinalBlendMask", new Vector4(
            hasNorth ? 1f : 0f,
            hasSouth ? 1f : 0f,
            hasEast ? 1f : 0f,
            hasWest ? 1f : 0f));
        shader.SetParameter("diagonalBlendMask", new Vector4(
            hasNorthEast ? 1f : 0f,
            hasNorthWest ? 1f : 0f,
            hasSouthEast ? 1f : 0f,
            hasSouthWest ? 1f : 0f));
        shader.SetParameter("spritePixelSize", GetPuddleLayerPixelSize(uid, sprite));
        shader.SetParameter("blendRangePixels", BlendRangePixels);

#if DEBUG
        _shaderDebugState[uid] = new PuddleShaderDebugState(
            color,
            northColor,
            southColor,
            eastColor,
            westColor,
            northEastColor,
            northWestColor,
            southEastColor,
            southWestColor);
#endif
    }

    private ShaderInstance GetShader(EntityUid uid, SpriteComponent sprite)
    {
        if (_puddleShaders.TryGetValue(uid, out var shader))
            return shader;

        shader = _prototype.Index(PuddleShader).InstanceUnique();
        sprite.LayerSetShader(0, shader, PuddleShader.Id);
        _puddleShaders.Add(uid, shader);

        return shader;
    }

    private void RemovePuddleShader(EntityUid uid, PuddleComponent puddle, SpriteComponent? sprite = null)
    {
        if (_spriteQuery.Resolve(uid, ref sprite, false))
            sprite.LayerSetShader(0, null, null);

        if (_puddleShaders.Remove(uid, out var shader))
            shader.Dispose();
#if DEBUG
        _shaderDebugState.Remove(uid);
#endif
    }

    private Color GetUnshadedPuddleColor(PuddleComponent puddle)
    {
        return puddle.SolutionColor;
    }

    private Vector2 GetPuddleLayerPixelSize(EntityUid uid, SpriteComponent sprite)
    {
        return _sprite.TryGetLayer((uid, sprite), 0, out var layer, false)
            ? layer.PixelSize
            : Vector2.One;
    }

    private Color GetPuddleColor(EntityUid uid)
    {
        return _puddleQuery.TryComp(uid, out var puddle) ? puddle.SolutionColor : Color.White;
    }

    private bool CanBlendPuddleColor(EntityUid uid)
    {
        return _iconSmoothQuery.TryComp(uid, out var smooth) && smooth.Enabled;
    }

    private void UpdatePuddleLocation(EntityUid uid, PuddleComponent puddle)
    {
        if (!_xformQuery.TryComp(uid, out var xform) ||
            xform.GridUid is not { } gridUid ||
            !_gridQuery.TryComp(gridUid, out var grid))
        {
            return;
        }

        puddle.LastPosition = (gridUid, _map.TileIndicesFor(gridUid, grid, xform.Coordinates));
    }

    private bool TryGetNeighbourColor(EntityUid uid, Direction direction, out Color color, EntityUid? ignored = null)
    {
        color = Color.White;

        if (!_xformQuery.TryComp(uid, out var xform) ||
            xform.GridUid is not { } gridUid ||
            !_gridQuery.TryComp(gridUid, out var grid))
        {
            return false;
        }

        var indices = _map.TileIndicesFor(gridUid, grid, xform.Coordinates).Offset(direction);
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, indices);

        while (anchored.MoveNext(out var ent))
        {
            if (ent.Value == ignored ||
                !_puddleQuery.TryComp(ent.Value, out var puddle) ||
                !CanBlendPuddleColor(ent.Value))
            {
                continue;
            }

            color = puddle.SolutionColor;
            return true;
        }

        return false;
    }

    private void UpdateNeighbourShaders(EntityUid uid, EntityUid? ignored = null, PuddleComponent? puddle = null)
    {
        if (!_xformQuery.TryComp(uid, out var xform) ||
            xform.GridUid is not { } gridUid ||
            !_gridQuery.TryComp(gridUid, out var grid))
        {
            if (puddle?.LastPosition is not { } lastPosition ||
                !_gridQuery.TryComp(lastPosition.GridUid, out var storedGrid))
            {
                return;
            }

            UpdateNeighbourShaders(lastPosition.GridUid, storedGrid, lastPosition.GridIndices, ignored);
            return;
        }

        var indices = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        UpdateNeighbourShaders(gridUid, grid, indices, ignored);
    }

    private void UpdateNeighbourShaders(EntityUid gridUid, MapGridComponent grid, Vector2i indices, EntityUid? ignored = null)
    {
        UpdatePuddlesAt(gridUid, grid, indices.Offset(Direction.North), ignored);
        UpdatePuddlesAt(gridUid, grid, indices.Offset(Direction.South), ignored);
        UpdatePuddlesAt(gridUid, grid, indices.Offset(Direction.East), ignored);
        UpdatePuddlesAt(gridUid, grid, indices.Offset(Direction.West), ignored);
        UpdatePuddlesAt(gridUid, grid, indices.Offset(Direction.NorthEast), ignored);
        UpdatePuddlesAt(gridUid, grid, indices.Offset(Direction.NorthWest), ignored);
        UpdatePuddlesAt(gridUid, grid, indices.Offset(Direction.SouthEast), ignored);
        UpdatePuddlesAt(gridUid, grid, indices.Offset(Direction.SouthWest), ignored);
    }

    private void UpdatePuddlesAt(EntityUid gridUid, MapGridComponent grid, Vector2i indices, EntityUid? ignored = null)
    {
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, indices);

        while (anchored.MoveNext(out var ent))
        {
            if (ent.Value != ignored && _puddleQuery.TryComp(ent.Value, out _))
                UpdatePuddleShader(ent.Value, ignored: ignored);
        }
    }

    #region Spill

    // Maybe someday we'll have clientside prediction for entity spawning, but not today.
    // Until then, these methods do nothing on the client.
    /// <inheritdoc/>
    public override bool TrySplashSpillAt(Entity<SpillableComponent?> entity, EntityCoordinates coordinates, out EntityUid puddleUid, out Solution solution, bool sound = true, EntityUid? user = null)
    {
        puddleUid = EntityUid.Invalid;
        solution = new Solution();
        return false;
    }

    public override bool TrySplashSpillAt(EntityUid entity,
        EntityCoordinates coordinates,
        Solution spilled,
        out EntityUid puddleUid,
        bool sound = true,
        EntityUid? user = null)
    {
        puddleUid = EntityUid.Invalid;
        return false;
    }

    /// <inheritdoc/>
    public override bool TrySpillAt(EntityCoordinates coordinates, Solution solution, out EntityUid puddleUid, bool sound = true)
    {
        puddleUid = EntityUid.Invalid;
        return false;
    }

    /// <inheritdoc/>
    public override bool TrySpillAt(EntityUid uid, Solution solution, out EntityUid puddleUid, bool sound = true, TransformComponent? transformComponent = null)
    {
        puddleUid = EntityUid.Invalid;
        return false;
    }

    /// <inheritdoc/>
    public override bool TrySpillAt(TileRef tileRef, Solution solution, out EntityUid puddleUid, bool sound = true, bool tileReact = true)
    {
        puddleUid = EntityUid.Invalid;
        return false;
    }

    #endregion Spill

#if DEBUG
    public readonly record struct PuddleShaderDebugState(
        Color Color,
        Color NorthColor,
        Color SouthColor,
        Color EastColor,
        Color WestColor,
        Color NorthEastColor,
        Color NorthWestColor,
        Color SouthEastColor,
        Color SouthWestColor);
#endif
}
