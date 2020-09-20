#nullable enable
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Maths;

namespace Content.Shared.GameObjects.EntitySystems
{
    public abstract class SharedRangedWeaponSystem : EntitySystem
    {
        public abstract void PlaySound(IEntity? user, IEntity weapon, string? sound, bool randomPitch = false);

        public abstract void MuzzleFlash(IEntity? user, IEntity weapon, string texture, Angle angle);

        public abstract void EjectCasing(IEntity? entity, IEntity casing, bool playSound = true, Direction[] ejectDirections = null);
    }
}