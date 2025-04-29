using Content.Shared.Atmos;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared.Disposal.Unit;

/// <summary>
/// "Abstract" entity that holds the gas mixture and has entities attached to it when flushed down disposals.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DisposalHolderComponent : Component, IGasMixtureHolder
{
    public Container Container = null!;

    /// <summary>
    ///     The total amount of time that it will take for this entity to
    ///     be pushed to the next tube
    /// </summary>
    [DataField, AutoNetworkedField]
    public float StartingTime { get; set; }

    /// <summary>
    ///     Time left until the entity is pushed to the next tube
    /// </summary>
    [ViewVariables]
    public float TimeLeft { get; set; }

    [ViewVariables]
    public EntityUid? PreviousTube { get; set; }

    [ViewVariables]
    public Direction PreviousDirection { get; set; } = Direction.Invalid;

    [ViewVariables]
    public Direction PreviousDirectionFrom => (PreviousDirection == Direction.Invalid) ? Direction.Invalid : PreviousDirection.GetOpposite();

    [ViewVariables]
    public EntityUid? CurrentTube { get; set; }

    [ViewVariables]
    public Direction CurrentDirection { get; set; } = Direction.Invalid;

    /// <summary>
    ///     A list of tags attached to the content, used for sorting
    /// </summary>
    [ViewVariables]
    public HashSet<string> Tags { get; set; } = new();

    [DataField]
    public GasMixture Air { get; set; } = new(70);
}
