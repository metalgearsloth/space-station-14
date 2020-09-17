using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Robust.Shared.GameObjects;

namespace Content.Server.GameObjects.Components.Weapon.Ranged.Ammunition
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedAmmoComponent))]
    internal sealed class AmmoComponent : SharedAmmoComponent
    {
    }
}
