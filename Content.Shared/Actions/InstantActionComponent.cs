using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Actions;

[RegisterComponent, NetworkedComponent]
public sealed partial class InstantActionComponent : BaseActionComponent
{

}

[Serializable, NetSerializable]
public sealed class InstantActionComponentState : BaseActionComponentState
{
    public InstantActionComponentState(InstantActionComponent component, IEntityManager entManager) : base(component, entManager)
    {
    }
}
