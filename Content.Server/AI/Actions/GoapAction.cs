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
        public HashSet<KeyValuePair<string, bool>> PreConditions = new HashSet<KeyValuePair<string, bool>>();
        public HashSet<KeyValuePair<string, bool>> Effects = new HashSet<KeyValuePair<string, bool>>();

        /// <summary>
        /// This should be set if TryPerformAction succeeds
        /// </summary>
        public bool IsDone => _isDone;
        protected bool _isDone = false;
        public bool InRange { get; set; } = false;

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

        public virtual void Reset()
        {
            TargetEntity = null;
            InRange = false;
        }

        /// <summary>
        /// This should check anything that's dynamic at runtime
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
