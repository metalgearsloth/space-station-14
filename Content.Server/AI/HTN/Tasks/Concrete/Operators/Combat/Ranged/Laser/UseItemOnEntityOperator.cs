using Content.Server.AI.HTN.Tasks.Concrete.Operators.Movement;
using Content.Server.AI.HTN.Tasks.Primitive.Operators;
using Content.Server.GameObjects.Components.Weapon.Ranged.Hitscan;
using Content.Server.GameObjects.EntitySystems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server.AI.HTN.Tasks.Concrete.Operators.Combat.Ranged.Laser
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
