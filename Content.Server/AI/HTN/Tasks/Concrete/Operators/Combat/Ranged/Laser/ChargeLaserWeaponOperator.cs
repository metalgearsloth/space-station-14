using Content.Server.AI.HTN.Tasks.Primitive.Operators.Movement;
using Content.Server.GameObjects.Components.Weapon.Ranged.Hitscan;
using Content.Server.GameObjects.EntitySystems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server.AI.HTN.Tasks.Primitive.Operators.Combat.Ranged.Laser
{
    public class ChargeLaserWeaponOperator : IOperator
    {

        private MoveToEntity _movementHandler;

        private IEntity _owner;
        private HitscanWeaponComponent _laserWeapon;
        private IEntity _charger;

        private bool _waitingForCharger = false;

        public ChargeLaserWeaponOperator(IEntity owner, HitscanWeaponComponent laserWeapon, IEntity charger)
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

            if (!_waitingForCharger)
            {
                var interactionSystem = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<InteractionSystem>();
                interactionSystem.UseItemInHand(_owner, _charger.Transform.GridPosition, _charger.Uid);
                _waitingForCharger = true;
            }

            return _laserWeapon.CapacitorComponent.Full ? Outcome.Success : Outcome.Continuing;
        }
    }
}
