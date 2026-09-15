using Robust.Shared.GameStates;

namespace Content.Shared.Throwing;

/// <summary>
/// When thrown applies a specific angle to the thrown entity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ThrowingAngleComponent : Component
{
    /// <summary>
    /// Do we apply throwing spin to the entity.
    /// </summary>
    [DataField("angularVelocity"), AutoNetworkedField]
    public bool AngularVelocity;

    [DataField("angle"), AutoNetworkedField]
    public Angle Angle;
}
