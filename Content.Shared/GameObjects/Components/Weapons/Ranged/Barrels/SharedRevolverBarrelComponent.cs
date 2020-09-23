#nullable enable
using System;
using System.Threading.Tasks;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.GameObjects.Components.Weapons.Ranged.Barrels
{
    public abstract class SharedRevolverBarrelComponent : SharedRangedWeaponComponent
    {
        public override string Name => "RevolverBarrel";
        public override uint? NetID => ContentNetIDs.REVOLVER_BARREL;

        public BallisticCaliber Caliber;
        
        /// <summary>
        ///     What slot will be used for the next bullet.
        /// </summary>
        protected ushort CurrentSlot = 0;

        protected abstract ushort Capacity { get; }

        public string? FillPrototype;
        
        /// <summary>
        ///     To avoid spawning entities in until necessary we'll just keep a counter for the unspawned default ammo.
        /// </summary>
        protected int UnspawnedCount;

        // Sounds
        protected string? SoundEject;
        protected string? SoundInsert;
        protected string? SoundSpin;

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);

            serializer.DataField(ref Caliber, "caliber", BallisticCaliber.Unspecified);
            serializer.DataField(ref FillPrototype, "fillPrototype", null);

            // Sounds
            serializer.DataField(ref SoundEject, "soundEject", "/Audio/Weapons/Guns/MagOut/revolver_magout.ogg");
            serializer.DataField(ref SoundInsert, "soundInsert", "/Audio/Weapons/Guns/MagIn/revolver_magin.ogg");
            serializer.DataField(ref SoundSpin, "soundSpin", "/Audio/Weapons/Guns/Misc/revolver_spin.ogg");
        }

        protected void Cycle()
        {
            // Move up a slot
            CurrentSlot = (ushort) ((CurrentSlot + 1) % Capacity);
        }

        // TODO: EJECTCASING should be on like a GunManager.

        /// <summary>
        ///     Dumps all cartridges onto the ground.
        /// </summary>
        /// <returns>The number of cartridges ejected</returns>
        protected abstract void EjectAllSlots();

        public virtual bool TryInsertBullet(IEntity user, SharedAmmoComponent ammoComponent)
        {
            if (ammoComponent.Caliber != Caliber)
                return false;

            return true;
        }
        
        public override async Task<bool> InteractUsing(InteractUsingEventArgs eventArgs)
        {
            if (!eventArgs.Using.TryGetComponent(out SharedAmmoComponent? ammoComponent))
            {
                return false;
            }

            return TryInsertBullet(eventArgs.User, ammoComponent);
        }

        public override bool UseEntity(UseEntityEventArgs eventArgs)
        {
            EjectAllSlots();
            return true;
        }
    }
    
    [Serializable, NetSerializable]
    public class RevolverBarrelComponentState : ComponentState
    {
        public ushort CurrentSlot { get; }
        public FireRateSelector FireRateSelector { get; }
        public bool?[] Bullets { get; }
        public string? SoundGunshot { get; }

        public RevolverBarrelComponentState(
            ushort currentSlot,
            FireRateSelector fireRateSelector,
            bool?[] bullets,
            string? soundGunshot) :
            base(ContentNetIDs.REVOLVER_BARREL)
        {
            CurrentSlot = currentSlot;
            FireRateSelector = fireRateSelector;
            Bullets = bullets;
            SoundGunshot = soundGunshot;
        }
    }
    
    [Serializable, NetSerializable]
    public sealed class ChangeSlotMessage : ComponentMessage
    {
        public ushort Slot { get; }
        
        public ChangeSlotMessage(ushort slot)
        {
            Slot = slot;
            Directed = true;
        }
    }
}