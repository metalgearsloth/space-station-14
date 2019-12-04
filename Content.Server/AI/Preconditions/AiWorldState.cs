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
            _aiStates.Add(new KeyValuePair<string, bool>(state, result));
        }

        public bool TryGetState(string state, out bool? result)
        {
            result = null;
            foreach (var (knownState, knownResult) in _aiStates)
            {
                if (knownState != state)
                {
                    continue;
                }
                result = knownResult;
                return true;
            }

            return false;
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
                            UpdateState("Hungry", true);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                };
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
                            UpdateState("EquippedHead", true);
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
                            break;
                        case EquipmentSlotDefines.Slots.IDCARD:
                            break;
                        case EquipmentSlotDefines.Slots.POCKET1:
                            break;
                        case EquipmentSlotDefines.Slots.POCKET2:
                            break;
                        case EquipmentSlotDefines.Slots.POCKET3:
                            break;
                        case EquipmentSlotDefines.Slots.POCKET4:
                            break;
                        case EquipmentSlotDefines.Slots.EXOSUITSLOT1:
                            break;
                        case EquipmentSlotDefines.Slots.EXOSUITSLOT2:
                            break;
                        default:
                            break;
                    }
                };
            }
        }

    }
}
