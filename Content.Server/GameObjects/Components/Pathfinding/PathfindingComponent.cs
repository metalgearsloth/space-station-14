using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Server.GameObjects.Components.Pathfinding
{
    public class PathfindingComponent : Component
    {
        public override string Name => "Pathfinding";
        public bool Traversable => _cost == 0;
        public double Cost => _cost;
        private double _cost;

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);
            serializer.DataField(ref _cost, "cost", 0);
        }
    }
}
