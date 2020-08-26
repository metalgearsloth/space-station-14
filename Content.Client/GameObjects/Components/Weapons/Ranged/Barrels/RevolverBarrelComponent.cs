using Content.Shared.GameObjects.Components.Weapons.Ranged;
using Robust.Client.GameObjects.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.GameObjects.Components;
using Robust.Shared.Interfaces.Random;
using Robust.Shared.IoC;

namespace Content.Client.GameObjects.Components.Weapons.Ranged.Barrels
{
    [RegisterComponent]
    public sealed class RevolverBarrelComponent : SharedRevolverBarrelComponent
    {
        protected override IContainer AmmoContainer { get; set; }
        
        public override void Initialize()
        {
            base.Initialize();
            Owner.GetComponent<ClientRangedWeaponComponent>().WeaponCanFireHandler += WeaponCanFire;
            Owner.GetComponent<ClientRangedWeaponComponent>().UserCanFireHandler += UserCanFire;
        }

        protected override void Shutdown()
        {
            base.Shutdown();
            Owner.GetComponent<ClientRangedWeaponComponent>().WeaponCanFireHandler -= WeaponCanFire;
            Owner.GetComponent<ClientRangedWeaponComponent>().UserCanFireHandler -= UserCanFire;
        }

        private bool WeaponCanFire()
        {
            return false;
        }

        private bool UserCanFire(IEntity entity)
        {
            return false;
        }
        
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
    }
}