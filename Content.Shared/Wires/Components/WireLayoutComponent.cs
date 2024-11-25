using Robust.Shared.GameStates;

namespace Content.Shared.Wires.Components;

/// <summary>
/// Component that stores wire layouts for the round.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WireLayoutComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<string, WireLayout> Layouts = new();
}
