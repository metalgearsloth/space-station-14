#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Content.Shared.GameObjects.EntitySystems;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Content.Shared.GameObjects.Components.Weapons.Ranged.Barrels
{
    public abstract class SharedMagazineBarrelComponent : SharedRangedWeaponComponent, IMagazineGun
    {
        public override string Name => "MagazineBarrel";

        public override uint? NetID => ContentNetIDs.MAGAZINE_BARREL;

        [ViewVariables]
        [DataField("magazineTypes")]
        public MagazineType MagazineTypes { get; private set; } = MagazineType.Unspecified;

        [ViewVariables]
        [DataField("caliber")]
        public BallisticCaliber Caliber { get; private set; } = BallisticCaliber.Unspecified;

        public int Capacity { get; set; }

        [ViewVariables]
        [DataField("magFillPrototype")]
        public EntityPrototype? MagFillPrototype { get; private set; }

        public SharedRangedMagazineComponent? Magazine { get; }

        [ViewVariables]
        [DataField("boltOpen")]
        public bool BoltOpen { get; set; }

        public SharedAmmoComponent? Chambered { get; }

        [ViewVariables]
        [DataField("autoEjectMag")]
        public bool AutoEjectMag { get; set; }

        [ViewVariables]
        [DataField("magazineRemovable")]
        public bool MagazineRemovable { get; } = false;

        [ViewVariables]
        [DataField("autoCycle")]
        public bool AutoCycle { get; } = false;

        // If the bolt needs to be open before we can insert / remove the mag (i.e. for LMGs)
        [ViewVariables]
        [DataField("magNeedsOpenBolt")]
        public bool MagNeedsOpenBolt { get; private set; }

        // Sounds
        [ViewVariables]
        [DataField("soundBoltOpen")]
        public string? SoundBoltOpen { get; private set; }

        [ViewVariables]
        [DataField("soundBoltClosed")]
        public string? SoundBoltClosed { get; private set; }

        [ViewVariables]
        [DataField("soundRack")]
        public string? SoundRack { get; private set; }

        [ViewVariables]
        [DataField("soundMagInsert")]
        public string? SoundMagInsert { get; private set; }

        [ViewVariables]
        [DataField("soundMagEject")]
        public string? SoundMagEject { get; private set; }

        [ViewVariables]
        [DataField("soundAutoEject")]
        public string? SoundAutoEject { get; private set; } = "/Audio/Weapons/Guns/EmptyAlarm/smg_empty_alarm.ogg";

        protected const float AutoEjectVariation = 0.1f;
        protected const float MagVariation = 0.1f;
        protected const float RackVariation = 0.1f;

        protected const float AutoEjectVolume = 0.0f;
        protected const float MagVolume = 0.0f;
        protected const float RackVolume = 0.0f;

        public abstract bool TryRemoveChambered();

        public abstract bool TryInsertChamber(SharedAmmoComponent ammo);

        public abstract bool TryRemoveMagazine();

        public abstract bool TryInsertMagazine(SharedRangedMagazineComponent magazine);

        public override bool UseEntity(UseEntityEventArgs eventArgs)
        {
            return UseEntity(eventArgs.User);
        }

        protected abstract bool UseEntity(IEntity user);

        protected abstract void RemoveMagazine(IEntity user);

        public override bool TryShoot(Angle angle)
        {
            if (!base.TryShoot(angle))
                return false;

            return !BoltOpen;
        }

        protected abstract bool TryInsertMag(IEntity user, IEntity mag);

        protected abstract bool TryInsertAmmo(IEntity user, IEntity ammo);

        public override async Task<bool> InteractUsing(InteractUsingEventArgs eventArgs)
        {
            if (TryInsertMag(eventArgs.User, eventArgs.Using))
            {
                return true;
            }

            if (TryInsertAmmo(eventArgs.User, eventArgs.Using))
            {
                return true;
            }

            return false;
        }
    }

    [Serializable, NetSerializable]
    public sealed class RemoveMagazineComponentMessage : ComponentMessage
    {
        public RemoveMagazineComponentMessage()
        {
            Directed = true;
        }
    }

    [Flags]
    public enum MagazineType
    {

        Unspecified = 0,
        LPistol = 1 << 0, // Placeholder?
        Pistol = 1 << 1,
        HCPistol = 1 << 2,
        Smg = 1 << 3,
        SmgTopMounted = 1 << 4,
        Rifle = 1 << 5,
        IH = 1 << 6, // Placeholder?
        Box = 1 << 7,
        Pan = 1 << 8,
        Dart = 1 << 9, // Placeholder
        CalicoTopMounted = 1 << 10,
    }

    [Serializable, NetSerializable]
    public enum AmmoVisuals
    {
        AmmoCount,
        AmmoMax,
        Spent,
    }

    [Serializable, NetSerializable]
    public enum MagazineBarrelVisuals
    {
        MagLoaded
    }

    [Serializable, NetSerializable]
    public enum BarrelBoltVisuals
    {
        BoltOpen,
    }

    [Serializable, NetSerializable]
    public class MagazineBarrelComponentState : ComponentState
    {
        public bool BoltOpen { get; }
        public bool? Chambered { get; }
        public FireRateSelector FireRateSelector { get; }
        public Stack<bool>? Magazine { get; }

        public MagazineBarrelComponentState(
            bool boltOpen,
            bool? chambered,
            FireRateSelector fireRateSelector,
            Stack<bool>? magazine) :
            base(ContentNetIDs.MAGAZINE_BARREL)
        {
            BoltOpen = boltOpen;
            Chambered = chambered;
            FireRateSelector = fireRateSelector;
            Magazine = magazine;
        }
    }
}
