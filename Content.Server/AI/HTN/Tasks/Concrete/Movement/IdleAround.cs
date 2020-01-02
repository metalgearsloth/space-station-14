using Content.Server.AI.HTN.WorldState;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Primitive.Movement
{
    public class IdleAround : ConcreteTask
    {
        public IdleAround(IEntity owner) : base(owner)
        {
        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            return true;
        }

        public override void SetupOperator()
        {
            throw new System.NotImplementedException();
        }
    }
}
