using Robust.Shared.GameStates;
using Robust.Shared.Map.Components;

namespace Content.Shared.Light.Components;

/// <summary>
/// Applies color to the <see cref="MapLightComponent"/> attached to all the member grid maps when this component is added.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class StationLightComponent : Component
{
    [DataField]
    public Color Color = Color.FromHex("#8589fa");
}
