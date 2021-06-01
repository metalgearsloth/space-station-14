using System.Diagnostics.CodeAnalysis;
using Content.Shared.GameObjects.Components.Projectiles;
using Content.Shared.GameObjects.Components.Weapons.Guns;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.GameObjects.Components.Weapon.Gun
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedBallisticMagazineComponent))]
    [ComponentReference(typeof(SharedAmmoProviderComponent))]
    [ComponentReference(typeof(SharedBallisticsAmmoProvider))]
    internal sealed class BallisticMagazineComponent : SharedBallisticMagazineComponent
    {
        public override int AmmoMax { get; }

        public Container AmmoContainer = default!;

        /// <inheritdoc />
        public override int AmmoCount => UnspawnedCount + AmmoContainer.ContainedEntities.Count;

        public override void Initialize()
        {
            base.Initialize();
            AmmoContainer = Owner.EnsureContainer<Container>("ammo", out var existing);

            if (existing)
            {
                UnspawnedCount -= AmmoContainer.ContainedEntities.Count;
            }

            DebugTools.Assert(AmmoCount <= AmmoCapacity);

            if (Owner.TryGetComponent(out SharedAppearanceComponent? appearanceComponent))
            {
                UpdateAppearance(appearanceComponent);
            }
        }

        public override bool TryGetProjectile([NotNullWhen(true)] out IProjectile? projectile)
        {
            if (!TryGetAmmo(out var ammo))
            {
                projectile = null;
                return false;
            }

            if (ammo.AmmoIsProjectile)
            {
                projectile = ammo;
                return true;
            }

            var protoManager = IoCManager.Resolve<IPrototypeManager>();

            if (ammo.IsHitscan(protoManager))
            {
                projectile = protoManager.Index<HitscanPrototype>(ammo.ProjectileId);
                return true;
            }
            else
            {
                projectile = Owner.EntityManager.SpawnEntity(ammo.ProjectileId, Owner.Transform.Coordinates).GetComponent<SharedProjectileComponent>();
                ammo.Spent = true;
                return true;
            }
        }

        public override bool TryGetAmmo([NotNullWhen(true)] out SharedAmmoComponent? ammo)
        {
            if (AmmoContainer.ContainedEntities.Count > 0)
            {
                var ammoEntity = AmmoContainer.ContainedEntities[0];

                ammo = ammoEntity.GetComponent<SharedAmmoComponent>();
                AmmoContainer.Remove(ammoEntity);
                return true;
            }

            if (UnspawnedCount > 0)
            {
                DebugTools.AssertNotNull(FillPrototype);
                UnspawnedCount--;
                var ammoEntity = Owner.EntityManager.SpawnEntity(FillPrototype, Owner.Transform.MapPosition);
                ammo = ammoEntity.GetComponent<SharedAmmoComponent>();
                return true;
            }

            ammo = null;
            return false;
        }
    }
}
