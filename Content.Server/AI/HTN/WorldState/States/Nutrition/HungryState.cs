using System;
using Content.Server.GameObjects.Components.Nutrition;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.WorldState.States.Nutrition
{
    public class HungryState : IStateData
    {
        public string Name => "Hungry";

        private IEntity _owner;
        public void Setup(IEntity owner)
        {
            _owner = owner;
        }

        public bool GetValue()
        {
            // TODO: Make this event driven and cache the value; this was just a shortcut
            if (!_owner.TryGetComponent(out HungerComponent hungerComponent))
            {
                return false;
            }

            switch (hungerComponent.CurrentHungerThreshold)
            {
                case HungerThreshold.Overfed:
                    return false;
                case HungerThreshold.Okay:
                    return false;
                case HungerThreshold.Peckish:
                    return true;
                case HungerThreshold.Starving:
                    return true;
                case HungerThreshold.Dead:
                    return true;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(hungerComponent.CurrentHungerThreshold),
                        hungerComponent.CurrentHungerThreshold,
                        null);
            }
        }
    }
}
