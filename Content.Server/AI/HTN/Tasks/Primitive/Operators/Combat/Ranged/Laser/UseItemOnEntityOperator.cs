using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Primitive.Operators.Combat.Ranged.Laser
{
    public class UseItemOnEntityOperator : IOperator
    {

        private IEntity _owner;
        private IEntity _itemToUse;
        private IEntity _useTarget;


        public UseItemOnEntityOperator(IEntity owner, IEntity itemToUse, IEntity useTarget)
        {
            _owner = owner;
            _itemToUse = itemToUse;
            _useTarget = useTarget;

        }

        public Outcome Execute(float frameTime)
        {
            // TODO
            return Outcome.Failed;
        }
    }
}
