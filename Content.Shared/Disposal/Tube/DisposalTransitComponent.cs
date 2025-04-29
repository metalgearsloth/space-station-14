using Content.Shared.Disposal.Unit;
using Robust.Shared.GameStates;

namespace Content.Server.Disposal.Tube
{
    // TODO: Different types of tubes eject in random direction with no exit point
    [RegisterComponent, NetworkedComponent]
    [Access(typeof(SharedDisposalTubeSystem))]
    public sealed partial class DisposalTransitComponent : Component
    {
    }
}
