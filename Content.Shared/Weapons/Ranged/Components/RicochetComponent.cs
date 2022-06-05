namespace Content.Shared.Weapons.Ranged.Components;

/// <summary>
/// Any projectiles with this may bounce off of surfaces
/// </summary>
[RegisterComponent]
public sealed class RicochetComponent : Component
{
    /// <summary>
    /// Chance of a reflection, 1 being guaranteed (where possible) and 0 being impossible.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("prob")]
    public float Prob = 1f;

    /// <summary>
    /// Track the entity we last hit for consecutive ricochets.
    /// </summary>
    [ViewVariables, DataField("lastRicochet")]
    public EntityUid? LastRicochet;
}
