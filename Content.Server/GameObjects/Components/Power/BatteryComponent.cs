#nullable enable
using System;
using Content.Shared.GameObjects.Components.Power;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Content.Server.GameObjects.Components.Power
{
    [RegisterComponent]
    public class BatteryComponent : SharedBatteryComponent
    {
        /// <summary>
        /// Maximum charge of the battery in joules (ie. watt seconds)
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)] public int MaxCharge { get => _maxCharge; set => SetMaxCharge(value); }
        [DataField("maxCharge")]
        private int _maxCharge = 1000;

        /// <summary>
        /// Current charge of the battery in joules (ie. watt seconds)
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public float CurrentCharge { get => _currentCharge; set => SetCurrentCharge(value); }
        [DataField("startingCharge")]
        private float _currentCharge = 500;

        /// <summary>
        /// True if the battery is fully charged.
        /// </summary>
        [ViewVariables] public bool IsFullyCharged => MathHelper.CloseTo(CurrentCharge, MaxCharge);

        [ViewVariables(VVAccess.ReadWrite)] [DataField("autoRecharge")] public bool AutoRecharge { get; set; }

        [ViewVariables(VVAccess.ReadWrite)] [DataField("autoRechargeRate")] public float AutoRechargeRate { get; set; }

        [ViewVariables] public BatteryState BatteryState { get; private set; }

        public override void Initialize()
        {
            base.Initialize();
            UpdateStorageState();
        }

        /// <summary>
        ///     If sufficient charge is available on the battery, use it. Otherwise, don't.
        /// </summary>
        public virtual bool TryUseCharge(float chargeToUse)
        {
            if (chargeToUse >= CurrentCharge)
            {
                return false;
            }

            CurrentCharge -= chargeToUse;
            return true;
        }

        public override float UseCharge(float toDeduct)
        {
            base.UseCharge(toDeduct);
            var chargeChangedBy = Math.Min(CurrentCharge, toDeduct);
            CurrentCharge -= chargeChangedBy;
            return chargeChangedBy;
        }

        public void FillFrom(BatteryComponent battery)
        {
            var powerDeficit = MaxCharge - CurrentCharge;
            if (battery.TryUseCharge(powerDeficit))
            {
                CurrentCharge += powerDeficit;
            }
            else
            {
                CurrentCharge += battery.CurrentCharge;
                battery.CurrentCharge = 0;
            }
        }

        protected virtual void OnChargeChanged() { }

        private void UpdateStorageState()
        {
            if (IsFullyCharged)
            {
                BatteryState = BatteryState.Full;
            }
            else if (CurrentCharge == 0)
            {
                BatteryState = BatteryState.Empty;
            }
            else
            {
                BatteryState = BatteryState.PartlyFull;
            }
        }

        private void SetMaxCharge(int newMax)
        {
            _maxCharge = Math.Max(newMax, 0);
            _currentCharge = Math.Min(_currentCharge, MaxCharge);
            UpdateStorageState();
            OnChargeChanged();
        }

        private void SetCurrentCharge(float newChargeAmount)
        {
            _currentCharge = MathHelper.Clamp(newChargeAmount, 0, MaxCharge);
            UpdateStorageState();
            OnChargeChanged();
        }

        public void OnUpdate(float frameTime)
        {
            if (!AutoRecharge) return;
            if (IsFullyCharged) return;
            CurrentCharge += AutoRechargeRate * frameTime;
        }
    }

    public enum BatteryState : byte
    {
        Full,
        PartlyFull,
        Empty
    }
}
