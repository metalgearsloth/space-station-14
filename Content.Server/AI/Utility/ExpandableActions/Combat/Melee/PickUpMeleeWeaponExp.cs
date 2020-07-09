using System.Collections.Generic;
using Content.Server.AI.Utility.Actions;
using Content.Server.AI.Utility.Actions.Combat.Melee;
using Content.Server.AI.WorldState;
using Content.Server.AI.WorldState.States;
using Content.Server.GameObjects.Components.Weapon.Melee;
using Content.Server.GameObjects.EntitySystems.AI.Sensory;
using Robust.Shared.GameObjects.Systems;

namespace Content.Server.AI.Utility.ExpandableActions.Combat.Melee
{
    public sealed class PickUpMeleeWeaponExp : ExpandableUtilityAction
    {
        public override float Bonus => UtilityAction.CombatPrepBonus;

        public override IEnumerable<UtilityAction> GetActions(Blackboard context)
        {
            var owner = context.GetState<SelfState>().GetValue();
            var sensor = EntitySystem.Get<AiSensorySystem>();

            foreach (var entity in sensor.GetNearestEntities<MeleeWeaponComponent>(owner))
            {
                yield return new MeleeWeaponAttackEntity(owner, entity, Bonus);
            }
        }
    }
}
