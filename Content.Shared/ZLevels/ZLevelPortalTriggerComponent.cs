using System.Numerics;
using Robust.Shared.Analyzers;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.ZLevels;

[RegisterComponent]
[Access(typeof(ZLevelPortalTriggerSystem))]
public sealed partial class ZLevelPortalTriggerComponent : Component
{
    /// <summary>
    /// The non-hard rectangular fixture that acts as the transition plane.
    /// </summary>
    [DataField(required: true)]
    public string TransitionFixture = string.Empty;

    /// <summary>
    /// Local direction in which the trigger must be crossed.
    /// </summary>
    [DataField(required: true)]
    public Vector2 Direction;

    /// <summary>
    /// Distance back toward the entrance required before a failed traversal may trigger again.
    /// </summary>
    [DataField]
    public float LatchResetDistance = 0.1f;
}
