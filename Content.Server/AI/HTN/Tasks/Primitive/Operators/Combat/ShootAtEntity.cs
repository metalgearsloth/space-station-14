using System;
using System.Collections.Generic;
using Content.Server.AI.HTN.WorldState;
using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Mobs;
using Content.Server.GameObjects.Components.Weapon.Ranged;
using Content.Server.GameObjects.EntitySystems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server.AI.HTN.Tasks.Primitive.Operators.Combat
{
    public class ShootAtEntity : IOperator
    {
        // Instance variables
        // How many shots before we pause
        private int _burstCount = 5;
        private int _burstCounter = 0;
        private float _pauseAmount = 0.5f;
        private float _pauseCounter;
        private ShotType _shotType = ShotType.WhileLOS;

        // Input variables
        private IEntity _owner;
        private IEntity _target;

        public ShootAtEntity(IEntity owner, IEntity target)
        {
            _owner = owner;
            _target = target;
        }

        public Outcome Execute(float frameTime)
        {
            if (!_owner.TryGetComponent(out CombatModeComponent combatModeComponent))
            {
                return Outcome.Failed;
            }

            // TODO: Check if we have LOS by chucking a ray out and if not and it's the first attempt use failed

            if (!combatModeComponent.IsInCombatMode)
            {
                combatModeComponent.IsInCombatMode = true;
            }

            if (!_owner.TryGetComponent(out HandsComponent hands) || hands.GetActiveHand == null)
            {
                return Outcome.Failed;
            }
            var rangedWeapon = hands.GetActiveHand.Owner;
            rangedWeapon.TryGetComponent(out RangedWeaponComponent rangedWeaponComponent);

            // TODO: Add a method to get shots left on RangedWeaponComponent

            // TODO: If out of processor range

            if (_burstCounter >= _burstCount)
            {
                if (_shotType == ShotType.OneBurst)
                {
                    return Outcome.Success;
                }
                _pauseCounter -= frameTime;
                if (_pauseCounter > 0)
                {
                    return Outcome.Continuing;
                }

            }

            _pauseCounter = _pauseAmount;
            _burstCounter = _burstCount;

            var interactionSystem = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<InteractionSystem>();
            interactionSystem.UseItemInHand(_owner, _target.Transform.GridPosition, _target.Uid);
            switch (_shotType)
            {
                case ShotType.WhileLOS:
                    return Outcome.Failed;
                case ShotType.OneBurst:
                    return Outcome.Failed;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public enum ShotType
    {
        WhileLOS,
        OneBurst,
    }
}
