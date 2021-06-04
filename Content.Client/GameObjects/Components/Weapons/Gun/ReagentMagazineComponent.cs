using System.Diagnostics.CodeAnalysis;
using Content.Shared.GameObjects.Components.Weapons.Guns;
using Robust.Shared.GameObjects;

namespace Content.Client.GameObjects.Components.Weapons.Gun
{
    [RegisterComponent]
    public class ReagentAmmoProviderComponent : SharedReagentAmmoProviderComponent
    {
        public override bool TryGetProjectile([NotNullWhen(true)] out IProjectile? projectile)
        {
            throw new System.NotImplementedException();
        }
    }
}
