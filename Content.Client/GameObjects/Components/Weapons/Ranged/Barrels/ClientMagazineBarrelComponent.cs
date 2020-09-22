#nullable enable
using System.Collections.Generic;
using Content.Shared.GameObjects.Components.Weapons.Ranged.Barrels;
using Content.Shared.GameObjects.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Localization;
using Robust.Shared.Utility;

namespace Content.Client.GameObjects.Components.Weapons.Ranged.Barrels
{
    [RegisterComponent]
    public sealed class ClientMagazineBarrelComponent : SharedMagazineBarrelComponent, IExamine
    {
        private bool? _chamber;

        private Stack<bool>? _magazine;
        
        private int ShotsLeft => 0;

        private void UpdateAppearance()
        {
            if (!Owner.TryGetComponent(out AppearanceComponent? appearanceComponent))
            {
                return;
            }
            
            appearanceComponent.SetData(BarrelBoltVisuals.BoltOpen, BoltOpen);
            appearanceComponent.SetData(MagazineBarrelVisuals.MagLoaded, _magazine != null);
            appearanceComponent.SetData(AmmoVisuals.AmmoCount, ShotsLeft);
            appearanceComponent.SetData(AmmoVisuals.AmmoMax, Capacity);
        }

        public override void HandleComponentState(ComponentState? curState, ComponentState? nextState)
        {
            base.HandleComponentState(curState, nextState);

            if (!(curState is MagazineBarrelComponentState cast))
            {
                return;
            }
            
            // TODO
        }

        protected override void Cycle(bool manual = false)
        {
            throw new System.NotImplementedException();
        }

        protected override void SetBolt(bool value)
        {
            throw new System.NotImplementedException();
        }

        protected override void TryEjectChamber()
        {
            _chamber = null;
        }

        protected override void TryFeedChamber()
        {
            if (_chamber != null)
            {
                return;
            }
            
            // Try and pull a round from the magazine to replace the chamber if possible
            if (_magazine == null || !_magazine.TryPop(out var nextCartridge))
            {
                return;
            }

            _chamber = nextCartridge;

            if (AutoEjectMag && _magazine != null && _magazine?.Count == 0)
            {
                EntitySystem.Get<SharedRangedWeaponSystem>().PlaySound(Shooter(), Owner, SoundAutoEject, true);
            }
        }

        void IExamine.Examine(FormattedMessage message, bool inDetailsInRange)
        {
            message.AddMarkup(Loc.GetString("\nIt uses [color=white]{0}[/color] ammo.", Caliber));

            foreach (var magazineType in GetMagazineTypes())
            {
                message.AddMarkup(Loc.GetString("\nIt accepts [color=white]{0}[/color] magazines.", magazineType));
            }
        }
    }
}