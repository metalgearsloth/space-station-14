#nullable enable
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Maths;

namespace Content.Shared.GameObjects.EntitySystems
{
    public abstract class SharedRangedWeaponSystem : EntitySystem
    {
        public abstract void PlaySound(IEntity? user, IEntity weapon, string? sound);

        public abstract void MuzzleFlash(IEntity? user, IEntity weapon, Angle? angle);
    }
}