using System.Diagnostics.CodeAnalysis;
using Content.Shared.GameObjects.Components.Weapons.Guns;
using Robust.Shared.GameObjects;
using Robust.Shared.Players;
using Robust.Shared.Utility;

namespace Content.Server.GameObjects.Components.Weapon.Gun
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedChamberedGunComponent))]
    [ComponentReference(typeof(SharedGunComponent))]
    internal sealed class ChamberedGunComponent : SharedChamberedGunComponent
    {
        public override ComponentState GetComponentState(ICommonSession player)
        {
            return new ChamberedGunComponentState(NextFire, Chamber.ContainedEntity != null, BoltClosed);
        }

        /// <summary>
        /// Tries to pop the currently chambered entity.
        /// </summary>
        /// <param name="ammo"></param>
        /// <returns></returns>
        public override bool TryPopChamber([NotNullWhen(true)] out SharedAmmoComponent? ammo)
        {
            var chambered = Chamber.ContainedEntity;

            if (chambered != null)
            {
                Chamber.Remove(chambered);
                ammo = chambered.GetComponent<SharedAmmoComponent>();
                Dirty();
                return true;
            }

            ammo = null;
            return false;
        }

        public override void TryFeedChamber()
        {
            if (Chamber.ContainedEntity != null) return;
            var magazine = MagazineSlot?.ContainedEntity;

            if (magazine == null) return;

            var ballistics = magazine.GetComponent<SharedBallisticsAmmoProvider>();

            if (ballistics.TryGetAmmo(out var ammo))
            {
                if (ballistics.Owner.TryGetComponent(out SharedAppearanceComponent? appearanceComponent))
                {
                    ballistics.UpdateAppearance(appearanceComponent);
                }

                DebugTools.AssertNotNull(ammo);
                Chamber.Insert(ammo.Owner);
                Dirty();
                ballistics.Dirty();
            }
        }
    }
}
