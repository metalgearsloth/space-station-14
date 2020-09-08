#nullable enable
using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Robust.Client.GameObjects.EntitySystems;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.Timing;
using Robust.Shared.IoC;

namespace Content.Client.GameObjects.Components.Weapons.Ranged
{
    public abstract class ClientRangedWeapon : SharedRangedWeapon
    {
        public override bool Firing
        {
            get => _firing;
            set
            {
                if (_firing == value)
                {
                    return;
                }

                _firing = value;

                if (_firing)
                {
                    NextFire = IoCManager.Resolve<IGameTiming>().CurTime;
                }
                
                SendNetworkMessage(new RangedFiringMessage(_firing));
            }
        }
        private bool _firing;
        
        protected override void PlaySound(string? sound)
        {
            if (sound == null)
                return;

            EntitySystem.Get<AudioSystem>().Play(sound, Owner);
        }
    }
}