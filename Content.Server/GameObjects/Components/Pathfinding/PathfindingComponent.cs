using System;
using Content.Server.GameObjects.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Server.GameObjects.Components.Pathfinding
{
    [RegisterComponent]
    public class PathfindingComponent : Component
    {
        public override string Name => "Pathfinding";
        public bool Traversable => Math.Abs(_cost) > 0.01;
        public float Cost => _cost;
        private float _cost;
        // When I profiled storing GridCoordinates seemed better than TileRef
        // (given 90% of nodes are walls which don't move their exact position it should also be better)
        internal GridCoordinates LastGrid { get; set; }

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);
            serializer.DataField(ref _cost, "cost", 0.0f);
        }

        // Potentialy todo, somewhat messy mixing the system doing shit and the component doing shit
        protected override void Startup()
        {
            base.Startup();
            var mapManager = IoCManager.Resolve<IMapManager>();
            LastGrid = Owner.Transform.GridPosition;
            var lastTile = mapManager.GetGrid(Owner.Transform.GridID).GetTileRef(LastGrid);

            if (Traversable)
            {
                PathfindingSystem.TileCosts.TryGetValue(lastTile, out var current);
                PathfindingSystem.TileCosts[lastTile] = current + Cost;
            }
            else
            {
                PathfindingSystem.BlockedTiles.TryGetValue(lastTile, out var current);
                PathfindingSystem.BlockedTiles[lastTile] = current + 1;
            }
        }

        protected override void Shutdown()
        {
            base.Shutdown();
            var mapManager = IoCManager.Resolve<IMapManager>();
            var lastTile = mapManager.GetGrid(Owner.Transform.GridID).GetTileRef(LastGrid);
            if (Traversable)
            {
                PathfindingSystem.TileCosts.TryGetValue(lastTile, out var current);
                PathfindingSystem.TileCosts[lastTile] = current - Cost;
            }
            else
            {
                PathfindingSystem.BlockedTiles.TryGetValue(lastTile, out var current);
                PathfindingSystem.BlockedTiles[lastTile] = current - 1;
            }
        }
    }
}
