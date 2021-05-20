using System.Diagnostics.CodeAnalysis;
using Content.Shared.GameObjects.Components.Weapons.Guns;
using Robust.Shared.GameObjects;

namespace Content.Server.GameObjects.Components.Weapon.Gun
{
    [RegisterComponent]
    public class BallisticMagazineComponent : SharedBallisticMagazineComponent
    {
        public override int AmmoCount { get; }
        public override int AmmoMax { get; }
        public override bool TryGetProjectile([NotNullWhen(true)] out IProjectile? projectile)
        {
            throw new System.NotImplementedException();
        }

        public override bool TryGetAmmo([NotNullWhen(true)] out SharedAmmoComponent? ammo)
        {
            throw new System.NotImplementedException();
        }
    }
}
