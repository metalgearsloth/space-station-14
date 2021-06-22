using Content.Shared.Flash.Guns;
using Content.Shared.GameObjects.Components.Weapons.Guns;
using Robust.Shared.GameObjects;
using Robust.Shared.Players;

namespace Content.Server.GameObjects.Components.Weapon.Gun
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedGunComponent))]
    internal sealed class GunComponent : SharedGunComponent
    {
    }
}
