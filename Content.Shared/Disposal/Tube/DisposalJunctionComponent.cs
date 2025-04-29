using Content.Shared.Disposal.Unit;
using Robust.Shared.GameStates;

namespace Content.Shared.Disposal.Tube;

[RegisterComponent, NetworkedComponent]
[Access(typeof(SharedDisposalTubeSystem))]
public sealed partial class DisposalJunctionComponent : Component
{
    /// <summary>
    ///     The angles to connect to.
    /// </summary>
    [DataField] public List<Angle> Degrees = new();
}
