using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;
using System.Collections.Generic;

namespace Content.Shared.Parallax
{
    [Prototype("parallax")]
    public sealed class ParallaxPrototype : IPrototype
    {
        [field: DataField("id")]
        public string ID { get; }

        [field: DataField("layers")]
        public List<ParallaxLayerDefinition> Layers { get; }
    }

    [DataDefinition]
    public sealed class ParallaxLayerDefinition
    {
        [DataField("sprite", readOnly: true)]
        public string Sprite;

        [DataField("state", readOnly: true)]
        public string State;

        [DataField("speed", readOnly: true)]
        public float speed = 0.0f;

        [DataField("chance", readOnly: true)]
        public float Chance = 0.0f;

        /// <summary>
        ///     Only 1 layer can be allowed for a particular key. This is useful for random layers so only 1 is chosen.
        /// </summary>
        [DataField("key", readOnly: true)]
        public string? Key = null;

        [DataField("randomColor", readOnly: true)]
        public bool RandomColor = false;

        [DataField("randomXOffset", readOnly: true)]
        public int RandomXOffset = 0;

        [DataField("randomYOffset", readOnly:true)]
        public int RandomYOffset = 0;
    }
}
