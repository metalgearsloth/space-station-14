using System;
using Content.Server.GameObjects.Components.Nutrition;
using Robust.Shared.Interfaces.GameObjects;
using ThirstComponent = Content.Server.GameObjects.Components.Nutrition.ThirstComponent;

namespace Content.Server.AI.HTN.WorldState.States.Nutrition
{
    public class ThirstyState : IStateData
    {
        public string Name => "Thirsty";

        private IEntity _owner;
        public void Setup(IEntity owner)
        {
            _owner = owner;
        }

        public bool GetValue()
        {
            // TODO: Make this event driven and cache the value; this was just a shortcut
            if (!_owner.TryGetComponent(out ThirstComponent thirstComponent))
            {
                return false;
            }

            switch (thirstComponent.CurrentThirstThreshold)
            {
                case ThirstThreshold.OverHydrated:
                    return false;
                case ThirstThreshold.Okay:
                    return false;
                case ThirstThreshold.Thirsty:
                    return true;
                case ThirstThreshold.Parched:
                    return true;
                case ThirstThreshold.Dead:
                    return true;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(thirstComponent.CurrentThirstThreshold),
                        thirstComponent.CurrentThirstThreshold,
                        null);
            }
        }
    }
}
