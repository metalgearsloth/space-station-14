using System;
using System.Collections.Generic;
using Content.Server.GameObjects;
using Content.Server.GameObjects.Components.Nutrition;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Utility;

namespace Content.Server.AI.Preconditions
{
    /// <summary>
    /// This essentially listens for events on the AiLogicController so rather than using CheckProceduralPreconditions everywhere you can cache things
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
            // UpdateStatus blah
        }

    }
}
