using Content.Server.AI.HTN.Tasks.Primitive.Operators.Inventory;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States;
using Content.Server.AI.HTN.WorldState.States.Combat;
using Content.Server.GameObjects;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Primitive.Combat
{
    public sealed class PickupNearestMeleeWeapon : ConcreteTask
    {
        private IEntity _nearestWeapon;
        public PickupNearestMeleeWeapon(IEntity owner) : base(owner)
        {

        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            var equippedWeapon = context.GetState<EquippedMeleeWeapon>().GetValue();
            if (equippedWeapon != null)
            {
                return false;
            }
            var nearbyWeapons = context.GetState<NearbyMeleeWeapons>();

            foreach (var entity in nearbyWeapons.GetValue())
            {
                // If someone already has it then skip
                if (!entity.TryGetComponent(out ItemComponent itemComponent) || itemComponent.IsEquipped) continue;
                _nearestWeapon = entity;
                return true;
            }

            return false;

        }

        public override void SetupOperator()
        {
            TaskOperator = new PickupEntity(Owner, _nearestWeapon);
        }
    }
}
