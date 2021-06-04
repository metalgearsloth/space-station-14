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
        public override bool TryPopChamber([NotNullWhen(true)] out SharedAmmoComponent? ammo)
        {
            ammo = null;
            return false;
        }

        public override void TryFeedChamber()
        {
            return;
        }
    }
}
