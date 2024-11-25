using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Wires.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WiresComponent : Component
{
    /// <summary>
    ///     The name of this entity's internal board.
    /// </summary>
    [DataField]
    public LocId BoardName = "wires-board-name-default";

    /// <summary>
    ///     The layout ID of this entity's wires.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<WireLayoutPrototype> LayoutId;

    /// <summary>
    ///     The serial number of this board. Randomly generated upon start,
    ///     does not need to be set.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? SerialNumber;

    /// <summary>
    ///     The seed that dictates the wires appearance, as well as
    ///     the status ordering on the UI client side.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int WireSeed;

    /// <summary>
    ///     The list of wires currently active on this entity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<Wire> WiresList { get; set; } = new();

    /// <summary>
    ///     Queue of wires saved while the wire's DoAfter event occurs, to prevent too much spam.
    /// </summary>
    [ViewVariables]
    public List<int> WiresQueue { get; } = new();

    /// <summary>
    ///     If this should follow the layout saved the first time the layout dictated by the
    ///     layout ID is generated, or if a new wire order should be generated every time.
    /// </summary>
    [DataField]
    public bool AlwaysRandomize { get; private set; }

    /// <summary>
    ///     Per wire status, keyed by an object.
    /// </summary>
    [ViewVariables]
    public Dictionary<object, object> Statuses { get; } = new();

    /// <summary>
    ///     The state data for the set of wires inside of this entity.
    ///     This is so that wire objects can be flyweighted between
    ///     entities without any issues.
    /// </summary>
    [ViewVariables]
    public Dictionary<object, object> StateData { get; } = new();

    [DataField]
    public SoundSpecifier PulseSound = new SoundPathSpecifier("/Audio/Effects/multitool_pulse.ogg");
}
