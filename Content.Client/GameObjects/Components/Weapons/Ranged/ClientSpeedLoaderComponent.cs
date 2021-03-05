#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Content.Shared.GameObjects.Components.Weapons.Ranged.Barrels;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.Client.GameObjects.Components.Weapons.Ranged
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedSpeedLoaderComponent))]
    public sealed class ClientSpeedLoaderComponent : SharedSpeedLoaderComponent
    {
        private Stack<bool> _spawnedAmmo = new Stack<bool>();

        public override int ShotsLeft => UnspawnedCount + _spawnedAmmo.Count;

        public override void Initialize()
        {
            base.Initialize();
            // At least until prediction is in we'll just assume an appearance
            if (FillPrototype == null)
            {
                UnspawnedCount = 0;
            }
            else
            {
                UnspawnedCount += Capacity;
            }

            UpdateAppearance();
        }

        public override bool TryInsertAmmo(IEntity user, SharedAmmoComponent ammoComponent)
        {
            // TODO
            return true;
        }

        protected override bool UseEntity(IEntity user)
        {
            // TODO
            return true;
        }

        protected override async Task<bool> AfterInteract(AfterInteractEventArgs eventArgs)
        {
            // TODO
            return true;
        }

        public override void HandleComponentState(ComponentState? curState, ComponentState? nextState)
        {
            base.HandleComponentState(curState, nextState);
            if (!(curState is SpeedLoaderComponentState cast))
            {
                return;
            }

            UnspawnedCount = 0;
            _spawnedAmmo = cast.Ammo;
            Capacity = cast.Capacity;
            UpdateAppearance();
        }

        private void UpdateAppearance()
        {
            if (!Owner.TryGetComponent(out AppearanceComponent? appearanceComponent))
            {
                return;
            }

            appearanceComponent?.SetData(MagazineBarrelVisuals.MagLoaded, true);
            appearanceComponent?.SetData(AmmoVisuals.AmmoCount, ShotsLeft);
            appearanceComponent?.SetData(AmmoVisuals.AmmoMax, Capacity);
        }
    }
}
