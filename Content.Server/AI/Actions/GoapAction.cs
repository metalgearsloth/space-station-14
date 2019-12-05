using System;
using System.Collections.Generic;
using Content.Server.AI.Preconditions;
using JetBrains.Annotations;
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
        /// <summary>
        /// This should only include known world states. If this isn't added to an IWorldState implementation then use CheckProceduralConditions
        /// </summary>
        public IDictionary<string, bool> PreConditions = new Dictionary<string, bool>();
        public IDictionary<string, bool> Effects = new Dictionary<string, bool>();

        /// <summary>
        /// This should be set if TryPerformAction succeeds
        /// </summary>
        public bool IsDone { get; set; }

        // Robust specific items
        // If TargetEntity is set that will take priority
        [CanBeNull] public IEntity TargetEntity { get; protected set; }
        public virtual GridCoordinates TargetGrid { get; protected set; }

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

        public virtual bool InRange(IEntity entity)
        {
            return !RequiresInRange;
        }

        public virtual void Reset()
        {
            TargetEntity = null;
            IsDone = false;
        }

        /// <summary>
        /// Checks if the preconditions for this action are met
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public virtual bool CheckProceduralPreconditions(IEntity entity)
        {
            return true;
        }

        /// <summary>
        /// Try and do the specified action, returning false if it fails for some reason
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public abstract bool TryPerformAction(IEntity entity);
    }
}
