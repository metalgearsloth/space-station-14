using System;
using System.Collections.Generic;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.AI.HTN.Blackboard
{
    public static class AiWorld
    {
        public enum WorldState
        {
            Owner,
            TargetEntity,

            // Visibility
            TargetNearby,

            // Combat
            CombatMode,

            // - Ranged
            RangedWeaponInHand,
            Shooting,

            // - Melee
            MeleeWeaponInHands,
            Swinging,

            // Hands
            FreeHands,

            // Inventory
            EquippedHead,
            EquippedGloves,

            // Movement
            Moving,
            HaveRoute,
            MoveToEntity,
            MoveToGrid,

            // Manipulation
            HasHands,

            // Nutrition
            HasFood,
            HasDrink,
            Hungry,
            Thirsty,
        }

        public static Dictionary<WorldState, Type> WorldStateTypes = new Dictionary<WorldState, Type>
        {
            {WorldState.Owner, typeof(IEntity)},
            {WorldState.TargetEntity, typeof(IEntity)},
            {WorldState.TargetNearby, typeof(IEntity)}
        };
    }
}
