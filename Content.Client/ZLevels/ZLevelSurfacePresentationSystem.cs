using Robust.Client.Graphics;

namespace Content.Client.ZLevels;

/// <summary>
/// Keeps the projected-surface overlay type available to support tooling without enabling coder-art in gameplay.
/// </summary>
public sealed partial class ZLevelSurfacePresentationSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay<ZLevelSurfaceOverlay>();
        base.Shutdown();
    }
}
