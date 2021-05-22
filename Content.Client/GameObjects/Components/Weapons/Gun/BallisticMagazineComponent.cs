using System.Diagnostics.CodeAnalysis;
using Content.Shared.GameObjects.Components.Weapons.Guns;
using Robust.Shared.GameObjects;

namespace Content.Client.GameObjects.Components.Weapons.Gun
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedBallisticMagazineComponent))]
    internal sealed class BallisticMagazineComponent : SharedBallisticMagazineComponent
    {
        public override int ProjectileCount => _projectileCount;
        private int _projectileCount;

        public override int AmmoCount { get; }
        public override int AmmoMax { get; }

        public override void Initialize()
        {
            base.Initialize();
            if (Owner.TryGetComponent(out SharedAppearanceComponent? appearanceComponent))
            {
                UpdateAppearance(appearanceComponent);
            }
        }

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
