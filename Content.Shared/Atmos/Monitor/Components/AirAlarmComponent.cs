using Content.Shared.Atmos.Piping.Unary.Components;
using Content.Shared.DeviceLinking;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Atmos.Monitor.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class AirAlarmComponent : Component
{
    [DataField] public AirAlarmMode CurrentMode { get; set; } = AirAlarmMode.Filtering;
    [DataField] public bool AutoMode { get; set; } = true;

    public readonly HashSet<string> KnownDevices = new();
    public readonly Dictionary<string, GasVentPumpData> VentData = new();
    public readonly Dictionary<string, GasVentScrubberData> ScrubberData = new();
    public readonly Dictionary<string, AtmosSensorData> SensorData = new();

    public bool CanSync = true;

    /// <summary>
    /// Previous alarm state for use with output ports.
    /// </summary>
    [DataField]
    public AtmosAlarmType State = AtmosAlarmType.Normal;

    /// <summary>
    /// The port that gets set to high while the alarm is in the danger state, and low when not.
    /// </summary>
    [DataField]
    public ProtoId<SourcePortPrototype> DangerPort = "AirDanger";

    /// <summary>
    /// The port that gets set to high while the alarm is in the warning state, and low when not.
    /// </summary>
    [DataField]
    public ProtoId<SourcePortPrototype> WarningPort = "AirWarning";

    /// <summary>
    /// The port that gets set to high while the alarm is in the normal state, and low when not.
    /// </summary>
    [DataField]
    public ProtoId<SourcePortPrototype> NormalPort = "AirNormal";
}
