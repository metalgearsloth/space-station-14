using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;

namespace Content.Shared.Fluids.Components
{
    /// <summary>
    /// Puddle on a floor
    /// </summary>
    [Access(typeof(SharedPuddleSystem))]
    public abstract partial class SharedPuddleComponent : Component
    {
        [DataField]
        public SoundSpecifier SpillSound = new SoundPathSpecifier("/Audio/Effects/Fluids/splat.ogg");

        [DataField]
        public FixedPoint2 OverflowVolume = FixedPoint2.New(50);

        [DataField("solution")] public string SolutionName = "puddle";

        /// <summary>
        /// Default minimum speed someone must be moving to slip for all reagents.
        /// </summary>
        [DataField]
        public float DefaultSlippery = 5.5f;

        [ViewVariables]
        public Entity<SolutionComponent>? Solution;
    }
}
