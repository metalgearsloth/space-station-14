using System;
using System.Collections.Generic;
using Content.Server.GameObjects;
using Content.Server.GameObjects.EntitySystems;
using Robust.Server.Interfaces.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.IoC;

namespace Content.Server.AI.Routines
{
    /// <summary>
    ///  Will try and find a nearby weapon of the desired type then move and get it.
    /// </summary>
    public class AcquireWeaponAiRoutine : AiRoutine
    {
        // Dependencies
        private IServerEntityManager _serverEntityManager;
        private MoveToEntityAiRoutine _mover;
        private DateTime _lastCheckForWeapons = DateTime.Now - TimeSpan.FromSeconds(_timeBetweenWeaponChecks);
        private const double _timeBetweenWeaponChecks = 5.0;

        public override bool RequiresMover => true;

        private IEntity _owner;

        public AcquireWeaponType RequiredWeaponType = AcquireWeaponType.Melee;
        public bool HasWeapon => _hasWeapon;
        private bool _hasWeapon = false;
        public IEntity NearestWeapon => _nearestWeapon;
        private IEntity _nearestWeapon;

        // TODO: DO THIS BETTER
        public override void InjectMover(MoveToEntityAiRoutine mover)
        {
            _mover = mover;
        }

        public override void Setup(IEntity owner)
        {
            base.Setup(owner);
            _owner = owner;
            _serverEntityManager = IoCManager.Resolve<IServerEntityManager>();
        }

        private bool ValidWeapon(IEntity weapon)
        {
            IDictionary<AcquireWeaponType, List<string>> weaponPrototypes =
                new Dictionary<AcquireWeaponType, List<string>>
                {
                    {AcquireWeaponType.Melee, new List<string>
                    {
                        "Spear"
                    }},
                };
            // TODO: Potentially use types instead?
            if (!weaponPrototypes[RequiredWeaponType].Contains(weapon.Prototype.ID))
            {
                return false;
            }

            // If someone's holding probs don't get it
            if (!weapon.TryGetComponent(out ItemComponent itemComponent))
            {
                return false;
            }

            if (itemComponent.IsEquipped)
            {
                return false;
            }

            return true;
        }

        private void FindNearestWeapon()
        {
            _lastCheckForWeapons = DateTime.Now;
            // First we'll try 10 tiles, otherwise just try the whole grid
            // TODO: Try entity's vision range instead, and if nothing in range move to another spot
            foreach (var entity in _serverEntityManager.GetEntitiesInRange(_owner, 50.0f))
            {
                if (!ValidWeapon(entity))
                {
                    continue;
                }

                _nearestWeapon = entity;
                return;
            }
            // Well shit
            // TODO: Check whole grid
        }

        private bool TryPickupWeapon(IEntity target)
        {
            if ((target.Transform.GridPosition.Position - _owner.Transform.GridPosition.Position).Length <
                InteractionSystem.InteractionRange)
            {
                _owner.TryGetComponent(out HandsComponent handsComponent);
                target.TryGetComponent(out ItemComponent itemComponent);
                handsComponent.PutInHand(itemComponent);
                if (handsComponent.GetActiveHand != itemComponent)
                {
                    handsComponent.SwapHands();
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// Will try and find a nearby weapon, then it will try and move to pick it up
        /// </summary>
        public void AcquireWeapon()
        {
            if (HasWeapon)
            {
                return;
            }

            // If we don't have a weapon or someone pinched it
            if (_nearestWeapon == null ||
                _nearestWeapon.TryGetComponent(out ItemComponent itemComponent) && itemComponent.IsEquipped)
            {
                if ((DateTime.Now - _lastCheckForWeapons).TotalSeconds < _timeBetweenWeaponChecks)
                {
                    return;
                }
                FindNearestWeapon();
            }

            // If we still didn't find anything
            if (_nearestWeapon == null)
            {
                return;
            }

            if (TryPickupWeapon(_nearestWeapon))
            {
                _hasWeapon = true;
                return;
            }

            if (_mover.Route.Count == 0)
            {
                _mover.TargetTolerance = InteractionSystem.InteractionRange - 0.01f;
                _mover.GetRoute(_nearestWeapon);
            }
            _mover.HandleMovement();
        }

        public override void Update()
        {
            base.Update();
            AcquireWeapon();
        }

    }

    public enum AcquireWeaponType
    {
        Melee,
        // Ranged
        Energy,
        Lmg,
    }
}
