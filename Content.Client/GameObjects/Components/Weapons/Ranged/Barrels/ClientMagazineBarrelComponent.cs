#nullable enable
using System.Collections.Generic;
using Content.Shared.Audio;
using Content.Shared.GameObjects.Components.Weapons.Ranged.Barrels;
using Content.Shared.GameObjects.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Client.GameObjects.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client.GameObjects.Components.Weapons.Ranged.Barrels
{
    [RegisterComponent]
    public sealed class ClientMagazineBarrelComponent : SharedMagazineBarrelComponent, IExamine
    {
        private bool? _chamber;

        private Stack<bool>? _magazine;
        
        private Queue<bool> _toFireAmmo = new Queue<bool>();
        
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
            _chamber = null;
            
            if (_magazine != null && _magazine.TryPop(out var next))
            {
                _chamber = next;
            }

            if (manual)
            {
                if (SoundCycle != null)
                {
                    EntitySystem.Get<AudioSystem>().Play(SoundCycle, Owner, AudioParams.Default.WithVolume(-2));
                }
            }
            
            UpdateAppearance();
        }

        protected override void SetBolt(bool value)
        {
            if (BoltOpen == value)
            {
                return;
            }

            if (value)
            {
                TryEjectChamber();
                if (SoundBoltOpen != null)
                {
                    EntitySystem.Get<AudioSystem>().Play(SoundBoltOpen, Owner, AudioHelpers.WithVariation(BoltToggleVariation));
                }
            }
            else
            {
                TryFeedChamber();
                if (SoundBoltClosed != null)
                {
                    EntitySystem.Get<AudioSystem>().Play(SoundBoltClosed, Owner, AudioHelpers.WithVariation(BoltToggleVariation));
                }
            }

            BoltOpen = value;
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

            if (AutoEjectMag && _magazine != null && _magazine.Count == 0 && SoundAutoEject != null)
            {
                EntitySystem.Get<AudioSystem>().Play(SoundAutoEject, Owner, AudioHelpers.WithVariation(AutoEjectVariation));
            }
        }

        protected override void RemoveMagazine(IEntity user)
        {
            _magazine = null;
        }

        protected override bool TryInsertMag(IEntity user, IEntity mag)
        {
            throw new System.NotImplementedException();
        }

        protected override bool TryInsertAmmo(IEntity user, IEntity ammo)
        {
            throw new System.NotImplementedException();
        }

        protected override bool UseEntity(IEntity user)
        {
            throw new System.NotImplementedException();
        }

        protected override bool TryTakeAmmo()
        {
            if (!base.TryTakeAmmo())
            {
                return false;
            }
            
            if (_chamber != null)
            {
                _toFireAmmo.Enqueue(_chamber.Value);
                Cycle();
                return true;
            }

            return false;
        }

        protected override void Shoot(int shotCount, Angle direction)
        {
            DebugTools.Assert(shotCount == _toFireAmmo.Count);

            while (_toFireAmmo.Count > 0)
            {
                var entity = _toFireAmmo.Dequeue();
                var sound = entity ? SoundGunshot : SoundEmpty;
                var variation = entity ? GunshotVariation : EmptyVariation;

                if (sound == null)
                    break;
                
                EntitySystem.Get<AudioSystem>().Play(sound, Owner, AudioHelpers.WithVariation(variation));
            }
            
            UpdateAppearance();
            // TODO: Status control
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