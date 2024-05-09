using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared.Movement.Events;

/// <summary>
/// Raised from client to server where the entity is owned by a particular client.
/// </summary>
[Serializable, NetSerializable]
public sealed class ClientMovementEvent : EntityEventArgs
{
    public NetEntity Entity;

    public Vector2 LocalPosition;
    public Angle LocalRotation;

    public Vector2 LinearVelocity;
    public float AngularVelocity;
}
