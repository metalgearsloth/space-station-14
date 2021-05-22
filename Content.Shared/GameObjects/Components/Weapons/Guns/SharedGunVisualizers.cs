using System;
using Robust.Shared.Serialization;

namespace Content.Shared.GameObjects.Components.Weapons.Guns
{
    [Serializable, NetSerializable]
    public enum GunVisuals : byte
    {
        MagLoaded,
        AmmoCount,
        AmmoMax,
        BoltClosed,
        AmmoSpent,
    }
}
