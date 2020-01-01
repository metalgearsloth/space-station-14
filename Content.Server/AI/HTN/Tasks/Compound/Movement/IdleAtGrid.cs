using System.Collections.Generic;
using Content.Server.AI.HTN.Tasks.Primitive.Movement;
using Content.Server.AI.HTN.WorldState;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.AI.HTN.Tasks.Compound.Movement
{
    /// <summary>
    /// Moves to target location and will continuously wander around the area
    /// </summary>
    public class IdleAtGrid : CompoundTask
    {
        private GridCoordinates _idleSpot;

        public IdleAtGrid(IEntity owner) : base(owner)
        {
            _idleSpot = Owner.Transform.GridPosition;
        }

        public IdleAtGrid(IEntity owner, GridCoordinates gridCoordinates) : base(owner)
        {
            _idleSpot = gridCoordinates;
        }

        public override string Name => "IdleAt";
        public override bool PreconditionsMet(AiWorldState context)
        {
            // TODO: Check Map as well
            if (_idleSpot.GridID != Owner.Transform.GridID)
            {
                return false;
            }

            return true;
        }

        public override void SetupMethods()
        {
            Methods = new List<IAiTask>()
            {
                new IdleAround(Owner),
                new MoveToGrid(Owner, _idleSpot),
            };
        }
    }
}
