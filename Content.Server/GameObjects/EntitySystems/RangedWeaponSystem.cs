using System;
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using JetBrains.Annotations;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.Timing;
using Robust.Shared.IoC;

namespace Content.Server.GameObjects.EntitySystems
{
    [UsedImplicitly]
    internal sealed class RangedWeaponSystem : EntitySystem
    {
        // Handles fire positions etc. from clients
        // It'd be cleaner to have this under corresponding Client / Server components buuuttt the issue with that is
        // you wouldn't be able to inherit from "SharedBlankWeapon" and would instead need to make
        // discrete server and client versions of each weapon that don't inherit from shared.
        
        // e.g. SharedRangedWeapon -> ServerRevolver and SharedRangedWeapon -> ClientRevolver
        // (Handles syncing via component)
        // vs.
        // SharedRangedWeapon -> SharedRevolver -> ServerRevolver
        // (needs to sync via system or component spaghetti)

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            var currentTime = IoCManager.Resolve<IGameTiming>().CurTime;
            
            foreach (var comp in ComponentManager.EntityQuery<SharedRangedWeapon>())
            {
                Update(comp, currentTime);
            }
        }
        
        private void Update(SharedRangedWeapon weapon, TimeSpan currentTime)
        {
            if (weapon.FiringStart == null && weapon.FiringEnd == null)
            {
                return;
            }
            
            if ((weapon.FiringStart != null && weapon.FiringStart > currentTime) && (weapon.FiringEnd != null && currentTime > weapon.FiringEnd))
            {
                return;
            }
            
            // TODO: Shitcode
            var shooter = weapon.Shooter();
            
            if (shooter == null)
            {
                return;
            }

            weapon.TryFire(currentTime, shooter, weapon.FireAngle!.Value);
        }
    }
}