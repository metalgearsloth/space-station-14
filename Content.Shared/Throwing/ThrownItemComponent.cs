using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Throwing
{
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
    public sealed partial class ThrownItemComponent : Component
    {
        /// <summary>
        /// Should the in-air throwing animation play.
        /// </summary>
        [DataField, AutoNetworkedField]
        public bool Animate = true;

        /// <summary>
        ///     The entity that threw this entity.
        /// </summary>
        [DataField, AutoNetworkedField]
        public EntityUid? Thrower;

        /// <summary>
        ///     The <see cref="IGameTiming.CurTime"/> timestamp at which this entity was thrown.
        /// </summary>
        [DataField, AutoNetworkedField]
        public TimeSpan? ThrownTime;

        /// <summary>
        ///     Compared to <see cref="IGameTiming.CurTime"/> to land this entity, if any.
        /// </summary>
        [DataField, AutoNetworkedField]
        [AutoPausedField]
        public TimeSpan? LandTime;

        /// <summary>
        ///     Whether or not this entity was already landed.
        /// </summary>
        [DataField, AutoNetworkedField]
        public bool Landed;

        /// <summary>
        /// Whether z-level physics controls this throw's landing.
        /// </summary>
        [DataField, AutoNetworkedField]
        public bool VerticalPhysics;

        /// <summary>
        ///     Whether or not to play a sound when the entity lands.
        /// </summary>
        [DataField, AutoNetworkedField]
        public bool PlayLandSound;

        /// <summary>
        ///     Used to restore state after the throwing scale animation is finished.
        /// </summary>
        [DataField]
        public Vector2? OriginalScale = null;
    }
}
