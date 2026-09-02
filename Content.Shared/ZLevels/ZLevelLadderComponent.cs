using System;
using Content.Shared.DoAfter;
using Robust.Shared.Analyzers;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.ZLevels;

/// <summary>
/// Adds delayed hand interaction to a z-portal endpoint.
/// </summary>
[RegisterComponent]
[Access(typeof(ZLevelLadderSystem))]
public sealed partial class ZLevelLadderComponent : Component
{
    [DataField]
    public TimeSpan ClimbDelay = TimeSpan.FromSeconds(1.5);
}

[Serializable, NetSerializable]
public sealed partial class ZLevelLadderTraverseDoAfterEvent : SimpleDoAfterEvent;
