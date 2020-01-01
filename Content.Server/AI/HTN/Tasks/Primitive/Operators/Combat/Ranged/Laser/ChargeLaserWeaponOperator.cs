using Content.Server.AI.HTN.Tasks.Primitive.Operators.Movement;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Tasks.Primitive.Operators.Combat.Ranged.Laser
{
    public class ChargeLaserWeaponOperator : IOperator
    {

        private MoveToEntity _movementHandler;

        private IEntity _owner;
        private IEntity _laserWeapon;
        private IEntity _charger;

        public ChargeLaserWeaponOperator(IEntity owner, IEntity laserWeapon, IEntity charger)
        {
            _owner = owner;
            _laserWeapon = laserWeapon;
            _charger = charger;

            _movementHandler = new MoveToEntity(_owner, _charger);
        }

        public Outcome Execute(float frameTime)
        {
            var movementOutcome = _movementHandler.Execute(frameTime);

            if (movementOutcome != Outcome.Success)
            {
                return Outcome.Continuing;
            }

            // TODO: Put item in charger, wait for it, then get it
            return Outcome.Failed;
        }
    }
}
