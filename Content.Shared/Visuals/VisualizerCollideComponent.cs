using Robust.Shared.GameStates;

namespace Content.Shared.Visuals;

[RegisterComponent, NetworkedComponent]
public sealed partial class VisualizerCollideComponent : Component
{
    [DataField(required: true)]
    public string Fixture = string.Empty;
}
