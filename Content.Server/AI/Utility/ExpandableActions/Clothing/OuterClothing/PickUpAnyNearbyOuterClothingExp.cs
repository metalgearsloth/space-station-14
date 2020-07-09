using System.Collections.Generic;
using Content.Server.AI.Utility.Actions;
using Content.Server.AI.Utility.Actions.Clothing.Gloves;
using Content.Server.AI.Utility.Actions.Clothing.OuterClothing;
using Content.Server.AI.WorldState;
using Content.Server.AI.WorldState.States;
using Content.Server.AI.WorldState.States.Clothing;
using Content.Server.GameObjects;
using Content.Server.GameObjects.EntitySystems.AI.Sensory;
using Content.Shared.GameObjects.Components.Inventory;
using Robust.Shared.GameObjects.Systems;

namespace Content.Server.AI.Utility.ExpandableActions.Clothing.OuterClothing
{
    public sealed class PickUpAnyNearbyOuterClothingExp : ExpandableUtilityAction
    {
        public override float Bonus => UtilityAction.NormalBonus;

        public override IEnumerable<UtilityAction> GetActions(Blackboard context)
        {
            var owner = context.GetState<SelfState>().GetValue();
            var sensor = EntitySystem.Get<AiSensorySystem>();

            foreach (var entity in sensor.GetNearestEntities<ClothingComponent>(owner))
            {
                if (entity.TryGetComponent(out ClothingComponent clothing) &&
                    (clothing.SlotFlags & EquipmentSlotDefines.SlotFlags.OUTERCLOTHING) != 0)
                {
                    yield return new PickUpOuterClothing(owner, entity, Bonus);
                }
            }
        }
    }
}
