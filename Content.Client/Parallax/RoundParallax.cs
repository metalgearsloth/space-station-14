#nullable enable
using System.Collections.Generic;

namespace Content.Client.Parallax
{
    // Is it a bad name? Yes.
    // But Parallax conflicts with the namespace and I wanted to dump everything into a class sooooooo
    internal sealed class RoundParallax
    {
        public IReadOnlyList<ParallaxLayer> Layers { get; }
    }
}
