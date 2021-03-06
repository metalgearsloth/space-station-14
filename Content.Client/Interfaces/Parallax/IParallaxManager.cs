using System;
using System.Collections.Generic;
using Content.Client.Parallax;
using Robust.Client.Graphics;

namespace Content.Client.Interfaces.Parallax
{
    public interface IParallaxManager
    {
        /// <summary>
        ///     All of the loaded parallax layers.
        /// </summary>
        IReadOnlyList<ParallaxLayer> ParallaxLayers { get; }

        void LoadParallax();
    }
}
