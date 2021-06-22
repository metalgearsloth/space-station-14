using System.Diagnostics.CodeAnalysis;
using Content.Shared.GameObjects.Components.Weapons.Guns;
using Robust.Shared.GameObjects;

namespace Content.Server.GameObjects.Components.Weapon.Gun
{
    [RegisterComponent]
    internal sealed class BatteryAmmoProviderComponent : SharedBatteryAmmoProviderComponent
    {
        public override bool TryGetProjectile([NotNullWhen(true)] out IProjectile? projectile)
        {
            throw new System.NotImplementedException();
        }
    }
}
