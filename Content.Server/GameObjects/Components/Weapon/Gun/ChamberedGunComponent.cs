using Content.Shared.GameObjects.Components.Weapons.Guns;
using Robust.Shared.GameObjects;

namespace Content.Server.GameObjects.Components.Weapon.Gun
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedChamberedGunComponent))]
    [ComponentReference(typeof(SharedGunComponent))]
    internal sealed class ChamberedGunComponent : SharedChamberedGunComponent
    {
    }
}
