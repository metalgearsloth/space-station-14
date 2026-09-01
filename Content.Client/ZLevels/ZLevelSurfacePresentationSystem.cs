using Robust.Client.Graphics;

namespace Content.Client.ZLevels;

/// <summary>
/// Owns the always-on, content-authored presentation of walkable high-ground surfaces.
/// </summary>
public sealed partial class ZLevelSurfacePresentationSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new ZLevelSurfaceOverlay(EntityManager));
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<ZLevelSurfaceOverlay>();
        base.Shutdown();
    }
}
