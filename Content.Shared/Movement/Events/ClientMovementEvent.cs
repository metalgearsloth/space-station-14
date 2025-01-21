using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared.Movement.Events;

[Serializable, NetSerializable]
public sealed class ClientMovementEvent : EntityEventArgs
{
    public Vector2 Position;
    public Angle Rotation;
}
