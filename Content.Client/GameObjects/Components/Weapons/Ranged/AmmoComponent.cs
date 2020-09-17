using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Robust.Shared.GameObjects;

namespace Content.Client.GameObjects.Components.Weapons.Ranged
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedAmmoComponent))]
    internal sealed class AmmoComponent : SharedAmmoComponent {}
}