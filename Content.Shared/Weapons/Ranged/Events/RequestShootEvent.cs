using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Weapons.Ranged.Events;

/// <summary>
/// Raised on the client to indicate it'd like to shoot.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestShootEvent : EntityEventArgs
{
    /// <summary>
    /// If the client is mousing over an entity send it in case we want to hit specific entities that would otherwise not collide.
    /// </summary>
    public EntityUid? TargetEntity;

    public EntityUid Gun;

    public EntityCoordinates Coordinates;
}
