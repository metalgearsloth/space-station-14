#nullable enable
using System;
using System.Collections.Generic;
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Content.Shared.GameObjects.Components.Weapons.Ranged.Barrels;
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
            UnspawnedCount = 0;
        }

        public override void HandleComponentState(ComponentState? curState, ComponentState? nextState)
        {
            base.HandleComponentState(curState, nextState);
            if (!(curState is SpeedLoaderComponentState cast))
            {
                return;
            }
            
            throw new InvalidOperationException();
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