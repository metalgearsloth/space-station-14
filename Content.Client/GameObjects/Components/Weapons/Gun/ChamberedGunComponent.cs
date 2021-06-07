using System.Diagnostics.CodeAnalysis;
using Content.Shared.GameObjects.Components.Weapons.Guns;
using Robust.Shared.GameObjects;

namespace Content.Client.GameObjects.Components.Weapons.Gun
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedChamberedGunComponent))]
    [ComponentReference(typeof(SharedGunComponent))]
    internal sealed class ChamberedGunComponent : SharedChamberedGunComponent
    {
        public bool? Chamber { get; set; }

        public override void Initialize()
        {
            base.Initialize();
            var mag = BallisticsMagazine;
            if (!string.IsNullOrEmpty(mag?.FillPrototype))
            {
                Chamber = true;
            }
        }

        public override void HandleComponentState(ComponentState? curState, ComponentState? nextState)
        {
            base.HandleComponentState(curState, nextState);
            if (curState is not ChamberedGunComponentState state) return;
            Chamber = state.Chamber;
            BoltClosed = state.BoltClosed;

            var mag = BallisticsMagazine;

            Owner.EntityManager.EventBus.RaiseLocalEvent(Owner.Uid, new AmmoUpdateEvent(Chamber == true, mag?.AmmoCount, mag?.AmmoMax));
            UpdateAppearance();
            Dirty();
        }

        public override bool CanFire()
        {
            return base.CanFire() && Chamber != null;
        }

        public override bool TryPopChamber([NotNullWhen(true)] out SharedAmmoComponent? ammo)
        {
            ammo = null;
            Chamber = null;
            return false;
        }

        public override void TryFeedChamber()
        {
            if (BallisticsMagazine is not BallisticMagazineComponent mag ||
                !mag.TryPopAmmo(out var ammo)) return;

            Chamber = ammo;
            return;
        }
    }
}
