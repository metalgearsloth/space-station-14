using System.Collections.Generic;
using Content.Server.AI.Utility.Actions;
using Content.Server.AI.Utility.Actions.Nutrition.Drink;
using Content.Server.AI.WorldState;
using Content.Server.AI.WorldState.States;
using Content.Server.AI.WorldState.States.Nutrition;
using Content.Server.GameObjects.Components.Nutrition;
using Content.Server.GameObjects.EntitySystems.AI.Sensory;
using Robust.Shared.GameObjects.Systems;

namespace Content.Server.AI.Utility.ExpandableActions.Nutrition
{
    public sealed class PickUpNearbyDrinkExp : ExpandableUtilityAction
    {
        public override float Bonus => UtilityAction.NeedsBonus;

        public override IEnumerable<UtilityAction> GetActions(Blackboard context)
        {
            var owner = context.GetState<SelfState>().GetValue();
            var sensor = EntitySystem.Get<AiSensorySystem>();

            foreach (var entity in sensor.GetNearestEntities<DrinkComponent>(owner))
            {
                yield return new PickUpDrink(owner, entity, Bonus);
            }
        }
    }
}
