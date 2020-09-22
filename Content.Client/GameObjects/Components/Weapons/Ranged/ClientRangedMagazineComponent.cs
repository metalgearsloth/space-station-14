#nullable enable
using System;
using System.Collections.Generic;
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Content.Shared.GameObjects.Components.Weapons.Ranged.Barrels;
using Content.Shared.GameObjects.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Utility;

namespace Content.Client.GameObjects.Components.Weapons.Ranged
{
    [RegisterComponent]
    [ComponentReference(typeof(SharedRangedMagazineComponent))]
    public sealed class ClientRangedMagazineComponent : SharedRangedMagazineComponent, IExamine
    {
        private Stack<bool> _spawnedAmmo = new Stack<bool>();

        public override int ShotsLeft => _spawnedAmmo.Count + UnspawnedCount;

        public override void Initialize()
        {
            base.Initialize();
            UnspawnedCount = 0;
        }

        private void UpdateAppearance()
        {
            if (!Owner.TryGetComponent(out AppearanceComponent? appearanceComponent))
            {
                return;
            }
            
            appearanceComponent.SetData(AmmoVisuals.AmmoCount, ShotsLeft);
            appearanceComponent.SetData(AmmoVisuals.AmmoMax, Capacity);
        }

        public override void HandleComponentState(ComponentState? curState, ComponentState? nextState)
        {
            base.HandleComponentState(curState, nextState);
            if (!(curState is RangedMagazineComponentState cast))
            {
                return;
            }
            
            throw new NotImplementedException();
        }

        public bool TryPop(out bool entity)
        {
            if (_spawnedAmmo.TryPop(out entity))
            {
                UpdateAppearance();
                return true;
            }

            return false;
        }

        protected override bool TryInsertAmmo(IEntity user, IEntity ammo)
        {
            throw new System.NotImplementedException();
        }

        protected override bool Use(IEntity user)
        {
            throw new System.NotImplementedException();
        }
        
        void IExamine.Examine(FormattedMessage message, bool inDetailsRange)
        {
            var text = Loc.GetString("It's a [color=white]{0}[/color] magazine of [color=white]{1}[/color] caliber.", MagazineType, Caliber);
            message.AddMarkup(text);
        }
    }
}