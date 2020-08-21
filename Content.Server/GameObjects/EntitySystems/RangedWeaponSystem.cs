using Content.Server.GameObjects.Components.Weapon.Ranged;
using Robust.Shared.GameObjects.Systems;

namespace Content.Server.GameObjects.EntitySystems
{
    public sealed class RangedWeaponSystem : EntitySystem
    {
        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            foreach (var comp in ComponentManager.EntityQuery<ServerRangedWeaponComponent>())
            {
                comp.Update(frameTime);
            }
        }
    }
}