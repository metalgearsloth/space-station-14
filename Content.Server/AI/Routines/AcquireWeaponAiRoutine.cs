using System;
using System.Collections.Generic;
using Content.Server.AI.Routines.Movers;
using Content.Server.GameObjects;
using Content.Server.GameObjects.EntitySystems;
using Robust.Server.AI;
using Robust.Server.Interfaces.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Random;
using Robust.Shared.IoC;
using Robust.Shared.Log;

namespace Content.Server.AI.Routines
{
    /// <summary>
    ///  Will try and find a nearby weapon of the desired type then move and get it.
    /// </summary>
    public class AcquireWeaponAiRoutine : AiRoutine
    {
        // Dependencies
#pragma warning disable 649
        [Dependency] private IServerEntityManager _serverEntityManager;
#pragma warning restore 649

        private MoveToEntityAiRoutine _mover = new MoveToEntityAiRoutine();

        public override bool RequiresMover => true;

        public AcquireWeaponType RequiredWeaponType = AcquireWeaponType.Melee;
        public bool HasWeapon => _hasWeapon;
        private bool _hasWeapon = false;
        public IEntity TargetWeapon => _targetWeapon;
        private IEntity _targetWeapon;

        protected override float ProcessCooldown { get; set; } = 1.0f;

        public override void Setup(IEntity owner)
        {
            base.Setup(owner);
            IoCManager.InjectDependencies(this);
            _mover.Setup(owner);
        }

        private bool ValidWeapon(IEntity weapon)
        {
            // TODO: Update this shite
            IDictionary<AcquireWeaponType, List<string>> weaponPrototypes =
                new Dictionary<AcquireWeaponType, List<string>>
                {
                    {AcquireWeaponType.Melee, new List<string>
                    {
                        "Spear"
                    }},
                    {AcquireWeaponType.Energy, new List<string>()
                    {
                        "LaserItem"
                    }},
                    {AcquireWeaponType.Smg, new List<string>()
                    {

                    }}
                };

            if (RequiredWeaponType == AcquireWeaponType.Any)
            {
                foreach (var category in weaponPrototypes)
                {
                    if (category.Value.Contains(weapon.Prototype.ID))
                    {
                        return true;
                    }
                }
            }

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

        private void FindRandomWeapon()
        {
            var foundWeapons = new List<IEntity>();

            foreach (var entity in _serverEntityManager.GetEntitiesInRange(Owner, Processor.VisionRadius))
            {
                if (!ValidWeapon(entity))
                {
                    continue;
                }

                foundWeapons.Add(entity);
            }

            // Well shit
            if (foundWeapons.Count == 0)
            {
                return;
            }

            var random = IoCManager.Resolve<IRobustRandom>();
            var randomWeapon = foundWeapons[random.Next(foundWeapons.Count - 1)];
            _targetWeapon = randomWeapon;
        }

        private bool TryPickupWeapon(IEntity target)
        {
            if ((target.Transform.GridPosition.Position - Owner.Transform.GridPosition.Position).Length <
                InteractionSystem.InteractionRange)
            {
                Owner.TryGetComponent(out HandsComponent handsComponent);
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
            // Checks frequency to avoid spamming searches
            if ((DateTime.Now - LastProcess).TotalSeconds < ProcessCooldown && (_targetWeapon == null ||
                _targetWeapon.TryGetComponent(out ItemComponent itemComponent) && itemComponent.IsEquipped))
            {
                FindRandomWeapon();
            }

            // If we still didn't find anything
            if (_targetWeapon == null)
            {
                return;
            }

            if (TryPickupWeapon(_targetWeapon))
            {
                _hasWeapon = true;
                return;
            }

            if (_mover.Arrived)
            {
                // Just to give some tolerance for the pathfinder
                const float weaponProximity = InteractionSystem.InteractionRange - 0.5f;
                _mover.TargetProximity = weaponProximity;
                _mover.GetRoute(_targetWeapon, weaponProximity);
            }
        }

        public override void Update()
        {
            base.Update();
            AcquireWeapon();
            _mover.HandleMovement();
        }

    }

    public enum AcquireWeaponType
    {
        Any,
        Melee,
        // Ranged
        Energy,
        Smg,
    }
}
