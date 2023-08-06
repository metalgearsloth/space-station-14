using Robust.Shared.GameStates;

namespace Content.Shared.Projectiles;

/// <summary>
/// Designates an entity as being the target of a projectile where the projectile will always collide with it.
/// e.g. Guns shooting crit mobs.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ProjectileTargetComponent : Component
{
    /*
     * Store the original collision mask / layer of the projectile so if we collide with any non-target ents
     * on our updated mask / layer we just ignore them.
     */

    [ViewVariables(VVAccess.ReadWrite), DataField("originalCollisionLayer"), AutoNetworkedField]
    public int OriginalCollisionLayer;

    [ViewVariables(VVAccess.ReadWrite), DataField("originalCollisionMask"), AutoNetworkedField]
    public int OriginalCollisionMask;

    [ViewVariables(VVAccess.ReadWrite), DataField("fixtureID")]
    public string FixtureID = SharedProjectileSystem.ProjectileFixture;

    [ViewVariables(VVAccess.ReadWrite), DataField("target"), AutoNetworkedField]
    public EntityUid? Target;
}
