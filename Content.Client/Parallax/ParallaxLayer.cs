#nullable enable
using Robust.Client.Graphics;
using Robust.Shared.Maths;

namespace Content.Client.Parallax
{
    /// <summary>
    ///     Texture and wrapper information for how fast the parallax moves in relation to the eye.
    /// </summary>
    public sealed class ParallaxLayer
    {
        /// <summary>
        ///     Set to 0 to make it static.
        /// </summary>
        public float Speed { get; set; } = 1f;

        public Texture ParallaxTexture { get; set; }

        public string Name { get; set; }

        public Vector2 Offset { get; set; }
    }
}
