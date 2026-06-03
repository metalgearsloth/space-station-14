using System.Numerics;
using System.Diagnostics.CodeAnalysis;
using Content.Client.IconSmoothing;
using Content.Client.Fluids.Components;
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
    private static readonly Color BaseColor = new(255, 255, 255, 128);
    private bool _puddleShadersEnabled;

    public override void Initialize()
    {
        base.Initialize();
        InitializeSharedPuddle<PuddleComponent>();

        SubscribeLocalEvent<PuddleComponent, AppearanceChangeEvent>(OnPuddleAppearance);
        SubscribeLocalEvent<PuddleComponent, ComponentShutdown>(OnPuddleShutdown);

        Subs.CVar(_cfg, CCVars.PuddleShaders, OnPuddleShadersChanged, true);
    }

    public override bool TryGetPuddle(EntityUid uid, [NotNullWhen(true)] out SharedPuddleComponent? puddle)
    {
        var found = _puddleQuery.TryComp(uid, out var clientPuddle);
        puddle = clientPuddle;
        return found;
    }

    protected override void TickEvaporation()
    {
        TickEvaporation(_puddleQuery);
    }

    private void OnPuddleAppearance(EntityUid uid, PuddleComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

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

        if (args.AppearanceData.TryGetValue(PuddleVisuals.SolutionColor, out var colorObj))
        {
            component.SolutionColor = (Color) colorObj;
        }
        else
        {
            component.SolutionColor = Color.White;
        }

        ApplyPuddleColor(uid, component, args.Sprite);
        UpdateNeighbourShaders(uid);
    }

    private void OnPuddleShutdown(EntityUid uid, PuddleComponent component, ComponentShutdown args)
    {
        RemovePuddleShader(uid, component);

        UpdateNeighbourShaders(uid, uid, component);
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

        var shader = GetShader(puddle, sprite);
        var color = GetPuddleColor(uid);
        UpdatePuddleLocation(uid, puddle);
        var canBlend = CanBlendPuddleColor(uid);
        var northColor = canBlend ? GetNeighbourColor(uid, Direction.North, color, ignored) : color;
        var southColor = canBlend ? GetNeighbourColor(uid, Direction.South, color, ignored) : color;
        var eastColor = canBlend ? GetNeighbourColor(uid, Direction.East, color, ignored) : color;
        var westColor = canBlend ? GetNeighbourColor(uid, Direction.West, color, ignored) : color;

        puddle.ShaderColor = color;
        puddle.NorthShaderColor = northColor;
        puddle.SouthShaderColor = southColor;
        puddle.EastShaderColor = eastColor;
        puddle.WestShaderColor = westColor;

        shader.SetParameter("color", color);
        shader.SetParameter("northColor", northColor);
        shader.SetParameter("southColor", southColor);
        shader.SetParameter("eastColor", eastColor);
        shader.SetParameter("westColor", westColor);
        shader.SetParameter("spritePixelSize", GetPuddleLayerPixelSize(uid, sprite));
    }

    private ShaderInstance GetShader(PuddleComponent puddle, SpriteComponent sprite)
    {
        if (puddle.Shader != null)
            return puddle.Shader;

        puddle.Shader = _prototype.Index(PuddleShader).InstanceUnique();
        sprite.LayerSetShader(0, puddle.Shader, PuddleShader.Id);
        return puddle.Shader;
    }

    private void RemovePuddleShader(EntityUid uid, PuddleComponent puddle, SpriteComponent? sprite = null)
    {
        if (_spriteQuery.Resolve(uid, ref sprite, false))
            sprite.LayerSetShader(0, null, null);

        puddle.Shader?.Dispose();
        puddle.Shader = null;
    }

    private Color GetUnshadedPuddleColor(PuddleComponent puddle)
    {
        return puddle.SolutionColor.WithAlpha(BaseColor.AByte);
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

    private bool UpdatePuddleLocation(EntityUid uid, PuddleComponent puddle)
    {
        if (!_xformQuery.TryComp(uid, out var xform) ||
            xform.GridUid is not { } gridUid ||
            !_gridQuery.TryComp(gridUid, out var grid))
        {
            return false;
        }

        puddle.GridUid = gridUid;
        puddle.GridIndices = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        return true;
    }

    private Color GetNeighbourColor(EntityUid uid, Direction direction, Color fallback, EntityUid? ignored = null)
    {
        if (!_xformQuery.TryComp(uid, out var xform) ||
            xform.GridUid is not { } gridUid ||
            !_gridQuery.TryComp(gridUid, out var grid))
        {
            return fallback;
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

            return puddle.SolutionColor;
        }

        return fallback;
    }

    private void UpdateNeighbourShaders(EntityUid uid, EntityUid? ignored = null, PuddleComponent? puddle = null)
    {
        if (!_xformQuery.TryComp(uid, out var xform) ||
            xform.GridUid is not { } gridUid ||
            !_gridQuery.TryComp(gridUid, out var grid))
        {
            if (puddle?.GridUid is not { } storedGridUid ||
                !_gridQuery.TryComp(storedGridUid, out var storedGrid))
            {
                return;
            }

            UpdateNeighbourShaders(storedGridUid, storedGrid, puddle.GridIndices, ignored);
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
}
