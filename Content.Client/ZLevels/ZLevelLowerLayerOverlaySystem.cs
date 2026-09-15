using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.ZLevels;

/// <summary>
/// Applies the visual effect for lower z-level layers.
/// </summary>
public sealed partial class ZLevelLowerLayerOverlaySystem : EntitySystem
{
    private static readonly Vector2 ProjectionOffset = new(0f, 0.7f);

    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private ZLevelSystem _zLevels = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new ZLevelLowerLayerOverlay(EntityManager, _prototypes, _zLevels));
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<ZLevelLowerLayerOverlay>();
        base.Shutdown();
    }

    [SubscribeLocalEvent]
    private static void OnVisualsRequested(
        Entity<ZLevelMapNetworkComponent> _,
        ref ZLevelVisualsEvent args)
    {
        args.ProjectionOffset = ProjectionOffset;
        args.StopAtOpaque = true;
    }
}

public sealed class ZLevelLowerLayerOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Shader = "ZLevelLowerLayer";
    private readonly IEntityManager _entities;
    private readonly ZLevelSystem _zLevels;
    private readonly ShaderInstance _shader;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture { get; set; } = true;
    public override bool OverwriteTargetFrameBuffer => true;

    public ZLevelLowerLayerOverlay(
        IEntityManager entities,
        IPrototypeManager prototypes,
        ZLevelSystem zLevels)
    {
        _entities = entities;
        _zLevels = zLevels;
        _shader = prototypes.Index(Shader).InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
        => TryGetEffects(args, out _);

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null || !TryGetEffects(args, out var effects))
            return;

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("blur_radius", 2f * effects.Strength);
        _shader.SetParameter("darken_strength", 0.3f * effects.Strength);
        _shader.SetParameter("blur_color", effects.AmbientColor);

        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }

    private bool TryGetEffects(in OverlayDrawArgs args, out ZLevelLowerLayerEffects effects)
    {
        effects = default;
        if (args.ZLevelOffset >= 0 ||
            !_zLevels.TryGetMapData(args.MapUid, out var zMap, out _))
        {
            return false;
        }

        var strength = args.Viewport.Eye?.RenderedAbsoluteZ is { } eyeZ
            ? Math.Clamp(eyeZ - zMap.Depth, 0f, 1f)
            : 1f;
        if (strength <= float.Epsilon)
            return false;

        var ambientColor = Vector3.UnitZ;
        if (_entities.TryGetComponent<MapLightComponent>(args.MapUid, out var mapLight))
        {
            ambientColor = new Vector3(
                mapLight.AmbientLightColor.R,
                mapLight.AmbientLightColor.G,
                mapLight.AmbientLightColor.B);
        }

        effects = new ZLevelLowerLayerEffects(strength, ambientColor);
        return true;
    }

    private readonly record struct ZLevelLowerLayerEffects(
        float Strength,
        Vector3 AmbientColor);
}
