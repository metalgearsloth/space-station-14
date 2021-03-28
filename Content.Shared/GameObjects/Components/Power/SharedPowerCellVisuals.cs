#nullable enable
using System;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.GameObjects.Components.Power
{
    public static class SharedPowerCellVisuals
    {
        public const int PowerCellVisualsLevels = 4;
    }

    // TODO: Dis
    public abstract class SharedBatteryComponent : Component
    {
        public override string Name => "Battery";

        public virtual float CurrentCharge { get; set; }
    }

    [Serializable, NetSerializable]
    public enum PowerCellVisuals
    {
        ChargeLevel
    }
}
