using System;
using Content.Server.AI.HTN.Tasks.Primitive.Operators.Combat.Ranged.Laser;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States.Combat;
using Content.Server.GameObjects.Components.Weapon.Ranged.Hitscan;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Primitive.Combat
{
    public class ChargeLaserWeapon : ConcreteTask
    {
        private HitscanWeaponComponent _equippedWeapon;
        private IEntity _nearbyCharger;

        public ChargeLaserWeapon(IEntity owner) : base(owner)
        {
        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            _equippedWeapon = context.GetState<EquippedLaserWeapon>().GetValue();
            if (_equippedWeapon == null || _equippedWeapon.CapacitorComponent.Full)
            {
                return false;
            }

            foreach (var charger in context.GetState<NearbyLaserChargers>().GetValue())
            {
                _nearbyCharger = charger;
            }

            return _nearbyCharger != null;
        }

        public override void SetupOperator()
        {
            TaskOperator = new ChargeLaserWeaponOperator(Owner, _equippedWeapon, _nearbyCharger);
        }
    }
}
