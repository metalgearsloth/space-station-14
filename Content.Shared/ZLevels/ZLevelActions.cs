using Content.Shared.Actions;

namespace Content.Shared.ZLevels;

/// <summary>
/// Raised when an observer explicitly moves up one linked z-level.
/// </summary>
public sealed partial class ZLevelGhostMoveUpActionEvent : InstantActionEvent;

/// <summary>
/// Raised when an observer explicitly moves down one linked z-level.
/// </summary>
public sealed partial class ZLevelGhostMoveDownActionEvent : InstantActionEvent;
