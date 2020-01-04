using Content.Server.AI.HTN.Tasks.Concrete.Inventory;
using Content.Server.AI.HTN.Tasks.Concrete.Movement;
using Content.Server.AI.HTN.Tasks.Primitive.Movement;
using Content.Server.AI.HTN.WorldState;
using Content.Server.AI.HTN.WorldState.States.Combat;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Sequence.Combat.Laser
{
    public class RechargeLaserWeapon : SequenceTask
    {
        public override string Name => "RechargeLaserWeapon";

        private IEntity _laserToCharge;
        private IEntity _laserCharger;

        public RechargeLaserWeapon(IEntity owner) : base(owner)
        {
        }

        public override bool PreconditionsMet(AiWorldState context)
        {

            foreach (var charger in context.GetEnumerableStateValue<NearbyLaserChargers, IEntity>())
            {
                _laserCharger = charger;
                return true;
            }

            return false;
        }

        public override void SetupSubTasks(AiWorldState context)
        {
            SubTasks = new IAiTask[]
            {
                // TODO UseEntityInWorld
                // TODO Wait for charge
                new UseItemOnEntity(Owner, _laserToCharge, _laserCharger),
                new MoveToEntity(Owner, _laserCharger),
            };
        }
    }
}
