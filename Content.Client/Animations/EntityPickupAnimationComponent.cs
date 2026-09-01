using Robust.Shared.Map;

namespace Content.Client.Animations;

/// <summary>
/// Applied to a client-side clone while it moves between independently presented coordinate spaces.
/// </summary>
[RegisterComponent]
[Access(typeof(EntityPickupAnimationSystem))]
public sealed partial class EntityPickupAnimationComponent : Component
{
    public TimeSpan StartTime;
    public float Duration;
    public EntityCoordinates StartCoordinates;
    public EntityCoordinates EndCoordinates;
    public EntityUid? Target;
    public EntityUid ReferenceMap;
    public int ReferenceDepth;
    public float StartAbsoluteZ;
    public Angle InitialAngle;
}
