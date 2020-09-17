using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Shared.GameObjects.Components.Weapons.Ranged.Barrels
{
    [Serializable, NetSerializable]
    public class RevolverBarrelComponentState : ComponentState
    {
        public ushort CurrentSlot { get; }
        public FireRateSelector FireRateSelector { get; }
        public bool?[] Bullets { get; }
        public string SoundGunshot { get; }

        public RevolverBarrelComponentState(
            ushort currentSlot,
            FireRateSelector fireRateSelector,
            bool?[] bullets,
            string soundGunshot) :
            base(ContentNetIDs.REVOLVER_BARREL)
        {
            CurrentSlot = currentSlot;
            FireRateSelector = fireRateSelector;
            Bullets = bullets;
            SoundGunshot = soundGunshot;
        }
    }
}
