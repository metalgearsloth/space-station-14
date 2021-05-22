using Content.Shared.GameObjects.Components.Weapons.Guns;
using Robust.Shared.GameObjects;

namespace Content.Client.GameObjects.Components.Weapons.Gun
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedAmmoComponent))]
    internal sealed class AmmoComponent : SharedAmmoComponent
    {

    }
}
