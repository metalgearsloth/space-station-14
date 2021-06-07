using System;
using System.Diagnostics.CodeAnalysis;
using Content.Shared.GameObjects.Components.Weapons.Guns;
using Robust.Shared.GameObjects;

namespace Content.Client.GameObjects.Components.Weapons.Gun
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedBallisticMagazineComponent))]
    [ComponentReference(typeof(SharedAmmoProviderComponent))]
    [ComponentReference(typeof(SharedBallisticsAmmoProvider))]
    internal sealed class BallisticMagazineComponent : SharedBallisticMagazineComponent
    {
        public bool? Chamber { get; set; }

        // TODO
        public bool HasMagazine { get; set; }

        public bool?[] Magazine { get; set; } = Array.Empty<bool?>();

        public override int AmmoCount
        {
            get
            {
                var count = 0;

                for (var i = 0; i < Magazine.Length; i++)
                {
                    if (Magazine[i] != null)
                    {
                        count++;
                        continue;
                    }

                    return count;
                }

                return count;
            }
        }

        public override void Initialize()
        {
            base.Initialize();
            if (Owner.TryGetComponent(out SharedAppearanceComponent? appearanceComponent))
            {
                UpdateAppearance(appearanceComponent);
            }

            Magazine = new bool?[AmmoMax];

            if (FillPrototype != null)
            {
                Magazine[^1] = null;
            }
        }

        public override void HandleComponentState(ComponentState? curState, ComponentState? nextState)
        {
            base.HandleComponentState(curState, nextState);
            if (curState is not BallisticMagazineComponentState state || nextState != null) return;

            var uiDirty = false;

            /* TODO
            if (_ammoCount != state.AmmoCount)
            {
                _ammoCount = state.AmmoCount;
                uiDirty = true;
            }
            */

            AmmoMax = state.AmmoMax;

            if (Owner.TryGetComponent(out SharedAppearanceComponent? appearanceComponent))
            {
                UpdateAppearance(appearanceComponent);
            }

            if (uiDirty)
            {
                Owner.EntityManager.EventBus.RaiseLocalEvent(Owner.Uid, new AmmoUpdateEvent(Chamber != null, AmmoCount, AmmoMax));
            }

            Dirty();
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

    internal sealed class AmmoUpdateEvent : EntityEventArgs
    {
        public bool Chambered { get; }
        public int? MagazineAmmo { get; }
        public int MagazineMax { get; }

        public AmmoUpdateEvent(bool chambered, int? magazineAmmo, int magazineMax)
        {
            Chambered = chambered;
            MagazineAmmo = magazineAmmo;
            MagazineMax = magazineMax;
        }
    }
}
