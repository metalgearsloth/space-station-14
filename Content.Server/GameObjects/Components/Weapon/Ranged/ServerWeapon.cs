using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Robust.Server.GameObjects.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.Network;
using Robust.Shared.Interfaces.Timing;
using Robust.Shared.IoC;
using Robust.Shared.Players;

namespace Content.Server.GameObjects.Components.Weapon.Ranged
{
    public abstract class ServerWeapon : SharedRangedWeapon
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
            }
        }
        private bool _firing;

        public override void HandleNetworkMessage(ComponentMessage message, INetChannel netChannel, ICommonSession? session = null)
        {
            base.HandleNetworkMessage(message, netChannel, session);

            // TODO: Verify player who sent it.
            
            switch (message)
            {
                case RangedFiringMessage msg:
                    Firing = msg.Firing;
                    break;
            }
        }

        protected override void PlaySound(string? sound)
        {
            if (sound == null)
                return;
            
            // TODO: Excluded
            EntitySystem.Get<AudioSystem>().PlayFromEntity(sound, Owner);
        }
    }
}