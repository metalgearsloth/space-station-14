using Content.Server.AI.HTN.Tasks.Primitive.Inventory;
using Content.Server.AI.HTN.Tasks.Primitive.Movement;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States.Combat;
using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Weapon.Ranged.Hitscan;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Sequence.Combat.Laser
{
    public sealed class PickupNearestLaserWeapon : SequenceTask
    {
        private IEntity _nearestWeapon;
        public PickupNearestLaserWeapon(IEntity owner) : base(owner)
        {

        }

        public override string Name { get; }

        public override bool PreconditionsMet(AiWorldState context)
        {
            var equippedWeapon = context.GetStateValue<EquippedLaserWeapon, HitscanWeaponComponent>();
            if (equippedWeapon != null)
            {
                return false;
            }

            foreach (var entity in context.GetEnumerableStateValue<NearbyLaserWeapons, IEntity>())
            {
                // If someone already has it then skip
                if (!entity.TryGetComponent(out ItemComponent itemComponent) || itemComponent.IsEquipped) continue;
                _nearestWeapon = entity;
                return true;
            }

            return false;

        }

        public override void SetupSubTasks(AiWorldState context)
        {
            SubTasks = new IAiTask[]
            {
                new PickupItem(Owner, _nearestWeapon),
                new MoveToEntity(Owner, _nearestWeapon),
            };
        }
    }
}
