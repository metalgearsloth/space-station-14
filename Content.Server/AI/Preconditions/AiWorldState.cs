using System;
using System.Collections.Generic;
using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Nutrition;
using Content.Shared.GameObjects.Components.Inventory;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Utility;

namespace Content.Server.AI.Preconditions
{
    /// <summary>
    /// This essentially listens for events on the AiLogicController so rather than using
    /// CheckProceduralPreconditions everywhere you can cache things via events
    /// </summary>
    public class AiWorldState : IWorldState
    {
        private IEntity _owner;
        private HashSet<KeyValuePair<string, bool>> _aiStates = new HashSet<KeyValuePair<string, bool>>();

        public event Action<string, bool> StateUpdate;

        public HashSet<KeyValuePair<string, bool>> GetState()
        {
            return _aiStates;
        }

        public AiWorldState(IEntity owner)
        {
            _owner = owner;
            RefreshWorldState();
            SetupListeners();
        }

        public void UpdateState(string state, bool result)
        {
            var newState = new KeyValuePair<string, bool>(state, result);
            if (_aiStates.Contains(newState))
            {
                return;
            }
            _aiStates.Add(newState);
            StateUpdate?.Invoke(state, result);
        }

        /// <summary>
        /// Will get a complete summary of the owner's known situation
        /// </summary>
        public void RefreshWorldState()
        {
            if (_owner.HasComponent<HandsComponent>())
            {
                UpdateState("HasHands", true);
            }

            if (_owner.HasComponent<StomachComponent>())
            {
                UpdateState("HasStomach", true);
            }

            if (_owner.TryGetComponent(out HungerComponent hungerComponent))
            {
                if (hungerComponent.CurrentHungerThreshold == HungerThreshold.Peckish ||
                    hungerComponent.CurrentHungerThreshold == HungerThreshold.Starving)
                {
                    UpdateState("Hungry", true);
                }
                else
                {
                    UpdateState("Hungry", false);
                }
            }

            if (_owner.TryGetComponent(out ThirstComponent thirstComponent))
            {
                if (thirstComponent.CurrentThirstThreshold == ThirstThreshold.Parched ||
                    thirstComponent.CurrentThirstThreshold == ThirstThreshold.Thirsty)
                {
                    UpdateState("Thirsty", true);
                }
                else
                {
                    UpdateState("Thirsty", false);
                }
            }
        }

        /// <summary>
        /// Goes through components and sets up listeners for event changes
        /// </summary>
        public void SetupListeners()
        {
            if (_owner.TryGetComponent(out HandsComponent handsComponent))
            {
                // TODO: What if food's in both hands genius
                handsComponent.PickedUp += entity =>
                {
                    if (entity.HasComponent<FoodComponent>())
                    {
                        UpdateState("HasFood", true);
                    }
                };

                handsComponent.Dropped += entity =>
                {
                    foreach (var item in handsComponent.GetAllHeldItems())
                    {
                        if (item.Owner.HasComponent<FoodComponent>())
                        {
                            UpdateState("HasFood", true);
                            return;
                        }
                        UpdateState("HasFood", false);
                    }
                };
            }

            // TODO: Stomach Add / Removal
            // Nutrition
            if (_owner.TryGetComponent(out HungerComponent hungerComponent))
            {
                hungerComponent.HungerThresholdChange += threshold =>
                {
                    switch (threshold)
                    {
                        case HungerThreshold.Overfed:
                            UpdateState("Hungry", false);
                            break;
                        case HungerThreshold.Okay:
                            UpdateState("Hungry", false);
                            break;
                        case HungerThreshold.Peckish:
                            UpdateState("Hungry", true);
                            break;
                        case HungerThreshold.Starving:
                            UpdateState("Hungry", true);
                            break;
                        case HungerThreshold.Dead:
                            UpdateState("Hungry", true);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                };
            }
            else
            {
                UpdateState("Hungry", false);
            }

            if (_owner.TryGetComponent(out ThirstComponent thirstComponent))
            {
                thirstComponent.ThirstThresholdChange += threshold =>
                {
                    switch (threshold)
                    {
                        case ThirstThreshold.OverHydrated:
                            UpdateState("Thirsty", false);
                            break;
                        case ThirstThreshold.Okay:
                            UpdateState("Thirsty", false);
                            break;
                        case ThirstThreshold.Thirsty:
                            UpdateState("Thirsty", true);
                            break;
                        case ThirstThreshold.Parched:
                            UpdateState("Thirsty", true);
                            break;
                        case ThirstThreshold.Dead:
                            UpdateState("Thirsty", true);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                };
            }
            else
            {
                UpdateState("Thirsty", false);
            }

            if (_owner.TryGetComponent(out InventoryComponent inventoryComponent))
            {
                inventoryComponent.EquippedClothing += args =>
                {
                    switch (args.Slot)
                    {
                        case EquipmentSlotDefines.Slots.BELT:
                            UpdateState("EquippedBelt", true);
                            break;
                        // Given there's so many equipment slots probs don't throw
                        case EquipmentSlotDefines.Slots.NONE:
                            break;
                        case EquipmentSlotDefines.Slots.HEAD:
                            UpdateState("EquippedHat", true);
                            break;
                        case EquipmentSlotDefines.Slots.EYES:
                            UpdateState("EquippedEyes", true);
                            break;
                        case EquipmentSlotDefines.Slots.EARS:
                            UpdateState("EquippedEars", true);
                            break;
                        case EquipmentSlotDefines.Slots.MASK:
                            UpdateState("EquippedMasks", true);
                            break;
                        case EquipmentSlotDefines.Slots.OUTERCLOTHING:
                            UpdateState("EquippedOuterClothing", true);
                            break;
                        case EquipmentSlotDefines.Slots.INNERCLOTHING:
                            UpdateState("EquippedInnerClothing", true);
                            break;
                        case EquipmentSlotDefines.Slots.BACKPACK:
                            UpdateState("EquippedBackpack", true);
                            break;
                        case EquipmentSlotDefines.Slots.GLOVES:
                            UpdateState("EquippedGloves", true);
                            break;
                        case EquipmentSlotDefines.Slots.SHOES:
                            UpdateState("EquippedShoes", true);
                            break;
                        default:
                            break;
                    }
                };
                inventoryComponent.UnequippedClothing += args =>
                {
                    switch (args.Slot)
                    {
                        case EquipmentSlotDefines.Slots.BELT:
                            UpdateState("EquippedBelt", false);
                            break;
                        // Given there's so many equipment slots probs don't throw
                        case EquipmentSlotDefines.Slots.NONE:
                            break;
                        case EquipmentSlotDefines.Slots.HEAD:
                            UpdateState("EquippedHat", false);
                            break;
                        case EquipmentSlotDefines.Slots.EYES:
                            UpdateState("EquippedEyes", false);
                            break;
                        case EquipmentSlotDefines.Slots.EARS:
                            UpdateState("EquippedEars", false);
                            break;
                        case EquipmentSlotDefines.Slots.MASK:
                            UpdateState("EquippedMasks", false);
                            break;
                        case EquipmentSlotDefines.Slots.OUTERCLOTHING:
                            UpdateState("EquippedOuterClothing", false);
                            break;
                        case EquipmentSlotDefines.Slots.INNERCLOTHING:
                            UpdateState("EquippedInnerClothing", false);
                            break;
                        case EquipmentSlotDefines.Slots.BACKPACK:
                            UpdateState("EquippedBackpack", false);
                            break;
                        case EquipmentSlotDefines.Slots.GLOVES:
                            UpdateState("EquippedGloves", false);
                            break;
                        case EquipmentSlotDefines.Slots.SHOES:
                            UpdateState("EquippedShoes", false);
                            break;
                        default:
                            break;
                    }
                };
            }
        }

    }
}
