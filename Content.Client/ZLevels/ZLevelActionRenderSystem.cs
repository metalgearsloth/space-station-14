using Content.Shared.ZLevels;
using Robust.Client.GameObjects;

namespace Content.Client.ZLevels;

/// <summary>
/// Renders explicit z-level actions as snapped layer changes.
/// </summary>
public sealed partial class ZLevelActionRenderSystem : EntitySystem
{
    [Dependency] private TransformSystem _transforms = default!;

    [EventSubscription]
    private void OnMoveUp(ZLevelGhostMoveUpActionEvent args)
    {
        _transforms.SnapRenderTransformAfterMapChange(args.Performer, true);
    }

    [EventSubscription]
    private void OnMoveDown(ZLevelGhostMoveDownActionEvent args)
    {
        _transforms.SnapRenderTransformAfterMapChange(args.Performer, true);
    }
}
