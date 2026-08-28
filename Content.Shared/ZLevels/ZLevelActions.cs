using Content.Shared.Actions;

namespace Content.Shared.ZLevels;

/// <summary>
/// Raised when using the move-up z-level action.
/// </summary>
public sealed partial class ZLevelGhostMoveUpActionEvent : InstantActionEvent;

/// <summary>
/// Raised when using the move-down z-level action.
/// </summary>
public sealed partial class ZLevelGhostMoveDownActionEvent : InstantActionEvent;
