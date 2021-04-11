#nullable enable
using System;
using System.Threading.Tasks;
using Content.Shared.GameObjects.EntitySystems;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Content.Shared.GameObjects.Components.Weapons.Ranged.Barrels
{
    public abstract class SharedRevolverBarrelComponent : SharedRangedWeaponComponent
    {
        public override string Name => "RevolverBarrel";
        public override uint? NetID => ContentNetIDs.REVOLVER_BARREL;

        [ViewVariables]
        [DataField("caliber")]
        public BallisticCaliber Caliber = BallisticCaliber.Unspecified;

        /// <summary>
        ///     What slot will be used for the next bullet.
        /// </summary>
        protected int CurrentSlot = 0;

        [ViewVariables]
        [DataField("capacity")]
        protected int Capacity { get; set; } = 6;

        [ViewVariables]
        [DataField("fillPrototype")]
        public string? FillPrototype;

        /// <summary>
        ///     To avoid spawning entities in until necessary we'll just keep a counter for the unspawned default ammo.
        /// </summary>
        protected int UnspawnedCount;

        // Sounds
        [ViewVariables]
        [DataField("soundEject")]
        public string? SoundEject { get; private set; } = "/Audio/Weapons/Guns/MagOut/revolver_magout.ogg";

        [ViewVariables]
        [DataField("soundInsert")]
        public string? SoundInsert { get; private set; } = "/Audio/Weapons/Guns/MagIn/revolver_magin.ogg";

        [ViewVariables]
        [DataField("soundSpin")]
        public string? SoundSpin { get; private set; } = "/Audio/Weapons/Guns/Misc/revolver_spin.ogg";

        protected const float SpinVariation = 0.1f;

        protected const float SpinVolume = 0.0f;

        protected void Cycle()
        {
            // Move up a slot
            CurrentSlot = (CurrentSlot + 1) % Capacity;
        }

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
        public int CurrentSlot { get; }
        public FireRateSelector FireRateSelector { get; }
        public bool?[] Bullets { get; }
        public string? SoundGunshot { get; }

        public RevolverBarrelComponentState(
            int currentSlot,
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
    public class ChangeSlotMessage : ComponentMessage
    {
        public int Slot { get; }

        public ChangeSlotMessage(int slot)
        {
            Slot = slot;
            Directed = true;
        }
    }

    [Serializable, NetSerializable]
    public sealed class RevolverSpinMessage : ChangeSlotMessage
    {
        public RevolverSpinMessage(int slot) : base(slot)
        {

        }
    }
}
