using Robust.Shared.GameObjects;

namespace Content.Shared.GameObjects.Components.Weapons.Guns
{
    /// <summary>
    /// Allows UI element to be displayed for tracking gun ammo.
    /// </summary>
    public abstract class SharedAmmoCounterComponent : Component
    {
        public override string Name => "AmmoCounter";
    }
}
