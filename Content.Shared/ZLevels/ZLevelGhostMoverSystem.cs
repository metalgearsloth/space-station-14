namespace Content.Shared.ZLevels;

/// <summary>
/// Handles the action controls for traversing a z-level map network.
/// </summary>
public sealed partial class ZLevelGhostMoverSystem : EntitySystem
{
    [Dependency] private ZLevelSystem _zLevels = default!;

    [EventSubscription]
    private void OnMoveUp(ZLevelGhostMoveUpActionEvent args)
    {
        if (!args.Handled)
            args.Handled = _zLevels.TryMoveEntityToMapOffset(args.Performer, 1);
    }

    [EventSubscription]
    private void OnMoveDown(ZLevelGhostMoveDownActionEvent args)
    {
        if (!args.Handled)
            args.Handled = _zLevels.TryMoveEntityToMapOffset(args.Performer, -1);
    }
}
