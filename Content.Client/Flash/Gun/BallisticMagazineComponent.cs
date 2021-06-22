using System;
using System.Diagnostics.CodeAnalysis;
using Content.Shared.Flash.Guns;
using Content.Shared.GameObjects.Components.Weapons.Guns;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.Client.GameObjects.Components.Weapons.Gun
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedBallisticMagazineComponent))]
    [ComponentReference(typeof(SharedAmmoProviderComponent))]
    [ComponentReference(typeof(SharedBallisticsAmmoProvider))]
    internal sealed class BallisticMagazineComponent : SharedBallisticMagazineComponent
    {
        // TODO
        public bool HasMagazine { get; set; }

        public override int AmmoCount => Magazine ?? 0;

        public int? Magazine { get; private set; }

        protected override void Initialize()
        {
            base.Initialize();
            if (Owner.TryGetComponent(out SharedAppearanceComponent? appearanceComponent))
            {
                UpdateAppearance(appearanceComponent);
            }

            if (FillPrototype != null)
            {
                Magazine = AmmoMax - 1;
            }
        }

        public override void HandleComponentState(ComponentState? curState, ComponentState? nextState)
        {
            base.HandleComponentState(curState, nextState);
            if (curState is not BallisticMagazineComponentState state || nextState != null) return;

            var uiDirty = false;

            if (AmmoMax != state.AmmoMax)
            {
                AmmoMax = state.AmmoMax;
                uiDirty = true;
            }

            // TODO: Desyncs and shit on this.
            if (AmmoCount != state.AmmoCount)
            {
                Magazine = state.AmmoCount;
                uiDirty = true;
            }

            if (Owner.TryGetComponent(out SharedAppearanceComponent? appearanceComponent))
            {
                UpdateAppearance(appearanceComponent);
            }

            if (uiDirty && Owner.TryGetContainerMan(out var man) && man.Owner.TryGetComponent(out ChamberedGunComponent? chamberedGun))
            {
                Owner.EntityManager.EventBus.RaiseLocalEvent(chamberedGun.Owner.Uid, new AmmoUpdateEvent(chamberedGun.Chamber != null, AmmoCount, AmmoMax));
            }

            Dirty();
        }

        public override bool TryGetProjectile([NotNullWhen(true)] out IProjectile? projectile)
        {
            throw new NotImplementedException();
        }

        public override bool TryGetAmmo([NotNullWhen(true)] out SharedAmmoComponent? ammo)
        {
            throw new NotImplementedException();
        }

        public bool TryPopAmmo([NotNullWhen(true)] out bool? ammo)
        {
            if (Magazine is null or <= 0)
            {
                ammo = null;
                return false;
            }

            ammo = true;
            Magazine--;
            return true;
        }
    }

    internal sealed class AmmoUpdateEvent : EntityEventArgs
    {
        public bool Chambered { get; }
        public int? MagazineAmmo { get; }
        public int? MagazineMax { get; }

        public AmmoUpdateEvent(bool chambered, int? magazineAmmo, int? magazineMax)
        {
            Chambered = chambered;
            MagazineAmmo = magazineAmmo;
            MagazineMax = magazineMax;
        }
    }
}
