using System;
using System.Collections.Generic;
using Content.Client.Interfaces.Parallax;
using Content.Client.Parallax;
using Robust.Client.Graphics;

namespace Content.IntegrationTests
{
    public sealed class DummyParallaxManager : IParallaxManager
    {
        public IReadOnlyList<ParallaxLayer> ParallaxLayers { get; }

        public void LoadParallax()
        {
        }
    }
}
