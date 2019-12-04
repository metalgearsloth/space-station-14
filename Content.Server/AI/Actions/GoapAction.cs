using System;
using System.Collections.Generic;
using Content.Server.AI.Preconditions;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.AI.Actions
{
    // Goal -> Available actions to satisfy that goal
    // Cost per action
    // Add procedural preconditions and effects
    // Eliminated add and delete lists for effects
    // C4

    // Goal: Stay Alive

    // Actions: MoveTo, Pickup Weapon (Precondition -> MoveTo), EquipWeapon (PreCondition -> Has Weapon)

    public abstract class GoapAction
    {
        public HashSet<KeyValuePair<AiState, bool>> PreConditions;
        public HashSet<KeyValuePair<AiState, bool>> Effects;

        public bool InRange { get; set; } = false;

        // Robust specific items
        public IEntity TargetEntity { get; set; }
        public virtual GridCoordinates? TargetGrid()
        {
            return TargetEntity?.Transform.GridPosition;
        }

        /// <summary>
        /// Gets the cost of the action. Can be generated dynamically
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public abstract float Cost();

        public virtual bool RequiresInRange { get; set; } = true;

        /// <summary>
        /// How close we have to be before this is complete
        /// </summary>
        public virtual float Range { get; set; } = 0.0f;

        //public AiAction(IEntity owner)
        //{
        //    Owner = owner;
        //}

        public virtual void Reset()
        {
            TargetEntity = null;
            InRange = false;
        }

        public virtual bool CheckProceduralPreconditions(GoapAgent agent)
        {
            return true;
        }

        public abstract bool TryPerformAction(GoapAgent agent);
    }
}
