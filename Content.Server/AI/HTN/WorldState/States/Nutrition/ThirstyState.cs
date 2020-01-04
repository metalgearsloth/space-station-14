using System;
using Content.Server.GameObjects.Components.Nutrition;
using Robust.Shared.Interfaces.GameObjects;
using ThirstComponent = Content.Server.GameObjects.Components.Nutrition.ThirstComponent;

namespace Content.Server.AI.HTN.WorldState.States.Nutrition
{
    [AiState]
    public class ThirstyState : StateData<bool?>
    {
        public override string Name => "Thirsty";

        public override bool? GetValue()
        {
            if (!Owner.TryGetComponent(out ThirstComponent thirstComponent))
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
