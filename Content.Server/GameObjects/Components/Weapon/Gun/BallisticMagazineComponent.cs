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
    public class BallisticMagazineComponent : SharedBallisticMagazineComponent
    {
        public override int AmmoCount { get; }
        public override int AmmoMax { get; }

        private Container? _ammoContainer = null;

        /// <inheritdoc />
        public override int ProjectileCount => UnspawnedCount + _ammoContainer?.ContainedEntities.Count ?? 0;

        public override void Initialize()
        {
            base.Initialize();
            _ammoContainer = Owner.EnsureContainer<Container>("ammo", out var existing);

            if (existing)
            {
                UnspawnedCount -= _ammoContainer.ContainedEntities.Count;
            }

            DebugTools.Assert(ProjectileCount <= ProjectileCapacity);

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
            if (_ammoContainer != null && _ammoContainer.ContainedEntities.Count > 0)
            {
                var ammoEntity = _ammoContainer.ContainedEntities[0];

                ammo = ammoEntity.GetComponent<SharedAmmoComponent>();
                _ammoContainer.Remove(ammoEntity);
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
