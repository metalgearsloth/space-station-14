namespace Content.Shared.ZLevels;

/// <summary>
/// Handles explicit observer traversal without opting observers into ordinary falling simulation.
/// </summary>
public sealed partial class ZLevelGhostMoverSystem : EntitySystem
{
    [Dependency] private Robust.Shared.Network.INetManager _net = default!;
    [Dependency] private ZLevelSystem _zLevels = default!;

    [EventSubscription]
    private void OnMoveUp(ZLevelGhostMoveUpActionEvent args)
    {
        if (!args.Handled && _net.IsServer)
            args.Handled = _zLevels.TryMoveEntityToMapOffset(args.Performer, 1);
    }

    [EventSubscription]
    private void OnMoveDown(ZLevelGhostMoveDownActionEvent args)
    {
        if (!args.Handled && _net.IsServer)
            args.Handled = _zLevels.TryMoveEntityToMapOffset(args.Performer, -1);
    }
}
