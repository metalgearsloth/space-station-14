using System;
using Robust.Shared.GameObjects;
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

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);
            serializer.DataField(ref _cost, "cost", 0.0f);
        }
    }
}
