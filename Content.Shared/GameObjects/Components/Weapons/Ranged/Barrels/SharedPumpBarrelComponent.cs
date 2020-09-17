using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;
using System;

namespace Content.Shared.GameObjects.Components.Weapons.Ranged.Barrels
{
    [Serializable, NetSerializable]
    public class PumpBarrelComponentState : ComponentState
    {
        public bool? Chamber { get; }
        public FireRateSelector FireRateSelector { get; }
        public (int count, int max)? Magazine { get; }
        public string SoundGunshot { get; }

        public PumpBarrelComponentState(
            bool? chamber,
            FireRateSelector fireRateSelector,
            (int count, int max)? magazine,
            string soundGunshot) :
            base(ContentNetIDs.PUMP_BARREL)
        {
            Chamber = chamber;
            FireRateSelector = fireRateSelector;
            Magazine = magazine;
            SoundGunshot = soundGunshot;
        }
    }
}
