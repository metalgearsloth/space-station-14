using System;
using System.Collections.Generic;
using Content.Server.GameObjects;
using Robust.Server.AI;
using Robust.Server.Interfaces.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Random;
using Robust.Shared.IoC;

namespace Content.Server.AI.Routines.Inventory
{
    /// <summary>
    ///  Will try and find a nearby item of the specified type then go and try to get it.
    /// This is mostly a higher-level wrapper around PickupItem
    /// </summary>
    public class AcquireItemRoutine : AiRoutine
    {
        // Dependencies
#pragma warning disable 649
        [Dependency] private IServerEntityManager _serverEntityManager;
#pragma warning restore 649

        private PickupItemRoutine _pickup = new PickupItemRoutine();

        public ItemCategories TargetCategory { get; set; }

        public IEntity TargetItem => _targetItem;
        private IEntity _targetItem;

        public bool HasItem => _pickup.HasItem();

        public SearchRoutine SearchRoutine { get; set; } = SearchRoutine.Nearest;

        protected override float ProcessCooldown { get; set; } = 1.0f;

        // TODO: Chuck in a yaml?
        public static IReadOnlyDictionary<ItemCategories, List<string>> ItemCategories => _itemCategories;

        private static readonly Dictionary<ItemCategories, List<string>> _itemCategories =
            new Dictionary<ItemCategories, List<string>>()
        {
            {
                Inventory.ItemCategories.AnyWeapon, new List<string>() {}
            },
            {
                Inventory.ItemCategories.Melee, new List<string>() {"Spear"}
            },
            {
                Inventory.ItemCategories.Sharp, new List<string>() {"Spear"}
            },
            {
                Inventory.ItemCategories.Ranged, new List<string>() {"LaserItem", "LCannon",}
            },
            {
                Inventory.ItemCategories.Energy, new List<string>() {"LaserItem", "LCannon",}
            },
            {
                Inventory.ItemCategories.Smg, new List<string>() {}
            }
        };

        public override void Setup(IEntity owner, AiLogicProcessor processor)
        {
            base.Setup(owner, processor);
            IoCManager.InjectDependencies(this);
            _pickup.Setup(owner, Processor);
        }

        private bool ValidItem(IEntity item)
        {
            if (!ItemCategories[TargetCategory].Contains(item.Prototype.ID))
            {
                return false;
            }

            if (!item.TryGetComponent(out ItemComponent itemComponent))
            {
                return false;
            }

            // If someone's holding probs don't get it
            if (itemComponent.IsEquipped)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets all nearby items of the category and picks the nearest
        /// </summary>
        private void FindNearestItem()
        {
            var foundWeapons = new List<IEntity>();

            foreach (var entity in _serverEntityManager.GetEntitiesInRange(Owner, Processor.VisionRadius))
            {
                if (!ValidItem(entity))
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

            IEntity nearest = foundWeapons[0];
            float nearestDistance = (nearest.Transform.GridPosition.Position - Owner.Transform.GridPosition.Position)
                .Length;;
            foreach (var weapon in foundWeapons)
            {
                var weaponDistance = (weapon.Transform.GridPosition.Position - Owner.Transform.GridPosition.Position)
                    .Length;
                if (weaponDistance < nearestDistance)
                {
                    nearest = weapon;
                    nearestDistance = weaponDistance;
                }
            }
            _targetItem = nearest;
        }

        /// <summary>
        /// Will take the first item that is found
        /// </summary>
        private void FindAnyItem()
        {
            foreach (var entity in _serverEntityManager.GetEntitiesInRange(Owner, Processor.VisionRadius))
            {
                if (!ValidItem(entity))
                {
                    continue;
                }

                _targetItem = entity;
                return;
            }
        }

        /// <summary>
        /// Gets all nearby items and returns a random one
        /// </summary>
        private void FindRandomItem()
        {
            var foundWeapons = new List<IEntity>();

            foreach (var entity in _serverEntityManager.GetEntitiesInRange(Owner, Processor.VisionRadius))
            {
                if (!ValidItem(entity))
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
            _targetItem = randomWeapon;
        }

        /// <summary>
        /// Will try and find a nearby item in the target category, then it will try and move to pick it up
        /// If it's already in use then it will skip it
        /// </summary>
        public void FindItem()
        {
            if (_pickup.HasItem())
            {
                return;
            }

            var oldItem = TargetItem;

            // If we don't have the item or someone's pinched it
            if (RemainingProcessCooldown <= 0 && (_targetItem == null ||
                (_targetItem.TryGetComponent(out ItemComponent itemComponent) && itemComponent.IsEquipped)))
            {
                switch (SearchRoutine)
                {
                    case SearchRoutine.Nearest:
                        FindNearestItem();
                        break;
                    case SearchRoutine.Random:
                        FindRandomItem();
                        break;
                    default:
                        FindAnyItem();
                        break;
                }
            }

            // If we still didn't find anything
            if (_targetItem == null)
            {
                return;
            }

            _pickup.ChangeItemTo(TargetItem);

            // If we don't need to change
            if (TargetItem == oldItem)
            {
                return;
            }
        }

        public override void Update(float frameTime)
        {
            FindItem();
            _pickup.GoPickupItem(frameTime);
            // Mover's handled in PickupItem
            base.Update(frameTime);
        }

    }

    public enum ItemCategories
    {
        AnyWeapon,
        // Melee combat
        Melee,
        Sharp,
        // Ranged combat
        Ranged,
        // Ranged
        Energy,
        Smg,
    }

    public enum SearchRoutine
    {
        Any,
        Nearest,
        Random,
    }
}
