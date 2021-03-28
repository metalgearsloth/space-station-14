#nullable enable
using System;
using Robust.Shared.Serialization;

namespace Content.Shared.GameObjects.Components.Power
{
    public static class SharedPowerCellVisuals
    {
        public const int PowerCellVisualsLevels = 4;
    }

    // TODO: Dis
    public abstract class SharedPowerCellComponent
    {

    }

    [Serializable, NetSerializable]
    public enum PowerCellVisuals
    {
        ChargeLevel
    }
}
