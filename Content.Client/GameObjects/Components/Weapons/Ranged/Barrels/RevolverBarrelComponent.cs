using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Robust.Client.GameObjects.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.GameObjects.Components;
using Robust.Shared.Interfaces.Random;
using Robust.Shared.IoC;
using Robust.Shared.Map;

namespace Content.Client.GameObjects.Components.Weapons.Ranged.Barrels
{
    [RegisterComponent]
    public sealed class RevolverBarrelComponent : SharedRevolverBarrelComponent
    {
        protected override IContainer AmmoContainer { get; set; }

        /// <summary>
        ///     Russian Roulette
        /// </summary>
        public void Spin()
        {
            var random = (ushort) IoCManager.Resolve<IRobustRandom>().Next(AmmoSlots.Length - 1);
            CurrentSlot = random;
            if (SoundSpin != null)
            {
                EntitySystem.Get<AudioSystem>().Play(SoundSpin, Owner.Transform.GridPosition, AudioParams.Default.WithVolume(-2));
            }
            
            // TODO: Send message
            // SendNetworkMessage();
        }

        protected override bool CanFire(IEntity entity)
        {
            if (!base.CanFire(entity))
            {
                return false;
            }

            return true;
        }

        public override void MuzzleFlash()
        {
            throw new System.NotImplementedException();
        }

        protected override bool TryTakeBullet()
        {
            // TODO.
        }

        protected override void Shoot(int shotCount, MapCoordinates fromPos, MapCoordinates toPos)
        {
            if (SoundGunshot != null)
            {
                var audioSystem = EntitySystem.Get<AudioSystem>();
                for (var i = 0; i < shotCount; i++)
                {
                    audioSystem.Play(SoundGunshot, Owner.Transform.GridPosition);
                }
            }
            
            // TODO: Take bullets.
        }
    }
}