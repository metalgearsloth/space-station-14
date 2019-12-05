using System;
using Content.Server.GameObjects.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.Interfaces.Network;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.ViewVariables;

namespace Content.Server.GameObjects.Components.Pathfinding
{
    [RegisterComponent]
    public class PathfindingComponent : Component
    {
        public override string Name => "Pathfinding";
        [ViewVariables]
        public bool Traversable { get; private set; }

        [ViewVariables]
        public float Cost
        {
            get => _cost;
            private set
            {
                _cost = value;
                Traversable = _cost > 0.01f;
            }
        }

        private float _cost;

        // When I profiled storing GridCoordinates seemed better than TileRef
        // (given 90% of nodes are walls which don't move their exact position it should also be better)
        internal GridCoordinates LastGrid { get; private set; }

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);
            var cost = 0.0f;
            serializer.DataField(ref cost, "cost", 1.0f);
            cost = Math.Max(cost, 0.0f);
            Cost = cost; // TODO: Surely there's an easier way to do this?
        }

        void HandleMove(GridCoordinates newGrid)
        {
            var entitySystemManager = IoCManager.Resolve<IEntitySystemManager>();
            var pathfindingSystem = entitySystemManager.GetEntitySystem<PathfindingSystem>();
            pathfindingSystem.HandleEntityChange(this);

            LastGrid = newGrid;
        }

        // Potentialy todo, somewhat messy mixing the system doing shit and the component doing shit
        protected override void Startup()
        {
            base.Startup();
            LastGrid = Owner.Transform.GridPosition;
            EntityAdd();
            Owner.Transform.OnMove += (sender, args) => { HandleMove(args.NewPosition);};
            // TODO: Do we need to call an event here?
        }

        protected override void Shutdown()
        {
            base.Shutdown();
            EntityRemove();
        }

        void EntityAdd()
        {
            var entitySystemManager = IoCManager.Resolve<IEntitySystemManager>();
            var pathfindingSystem = entitySystemManager.GetEntitySystem<PathfindingSystem>();
            pathfindingSystem.HandleEntityAdd(this);
        }

        void EntityRemove()
        {
            var entitySystemManager = IoCManager.Resolve<IEntitySystemManager>();
            var pathfindingSystem = entitySystemManager.GetEntitySystem<PathfindingSystem>();
            pathfindingSystem.HandleEntityRemove(this);
        }
    }
}
