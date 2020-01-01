using System.Collections.Generic;
using Content.Server.AI.HTN.Tasks.Primitive.Operators.Inventory;
using Content.Server.AI.HTN.WorldState;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Primitive.Nutrition
{
    public sealed class UseFoodInInventory : PrimitiveTask
    {
        private IEntity _targetFood;

        public UseFoodInInventory(IEntity owner) : base(owner)
        {

        }

        public override bool PreconditionsMet(AiWorldState context)
        {
            return false;
        }

        public override HashSet<IStateData> ProceduralEffects => new HashSet<IStateData>
        {
            // new HungryState(false)
        };

        public override void SetupOperator()
        {
            // TODO
            TaskOperator = new UseItemOnPerson(Owner, _targetFood);
        }
    }
}
