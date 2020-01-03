using Content.Server.AI.HTN.Tasks.Concrete.Inventory;
using Content.Server.AI.HTN.Tasks.Concrete.Movement;
using Content.Server.AI.HTN.Tasks.Primitive.Movement;
using Content.Server.AI.HTN.WorldState;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Sequence.Inventory
{
    public class GoPickupItem : SequenceTask
    {
        public override string Name => "PickupItem";

        private IEntity _target;

        public GoPickupItem(IEntity owner, IEntity target) : base(owner)
        {
            _target = target;
        }


        public override void SetupSubTasks(AiWorldState context)
        {
            SubTasks = new IAiTask[]
            {
                new MoveToEntity(Owner, _target),
                new PickupItem(Owner, _target),
            };
        }
    }
}
