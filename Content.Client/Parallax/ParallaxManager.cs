#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Content.Client.Interfaces.Parallax;
using Content.Shared;
using Nett;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client.Parallax
{
    internal sealed class ParallaxManager : IParallaxManager
    {
        [Dependency] private readonly IResourceCache _resourceCache = default!;
        [Dependency] private readonly ILogManager _logManager = default!;
        [Dependency] private readonly IConfigurationManager _configurationManager = default!;

        private static readonly ResourcePath ParallaxConfigPath = new("/parallax_config.toml");

        public IReadOnlyList<ParallaxLayer> ParallaxLayers => _parallaxLayers;

        private List<ParallaxLayer> _parallaxLayers = new();

        // TODO: Servers should be able to send config for someone loading in.
        public async void LoadParallax()
        {
            return;
            // TODO: Need to handle this; for now just log warning.
            if (!_configurationManager.GetCVar(CCVars.ParallaxMode)) return;

            if (!_resourceCache.TryContentFileRead(ParallaxConfigPath, out var configStream))
            {
                Logger.ErrorS("parallax", "Parallax config not found.");
                return;
            }

            string contents;
            TomlTable config;

            using (configStream)
            {
                using (var reader = new StreamReader(configStream, EncodingHelpers.UTF8))
                {
                    contents = reader.ReadToEnd().Replace(Environment.NewLine, "\n");
                }

                config = Toml.ReadString(contents);
            }

            var sawmill = _logManager.GetSawmill("parallax");

            foreach (var layerConfig in ((TomlTableArray) config.Get("layers")).Items)
            {
                var layer = await LoadLayer(layerConfig, sawmill);
                _parallaxLayers.Add(layer);
            }

            sawmill.Info($"Loaded {_parallaxLayers.Count} parallax layers.");
        }

        // TODO: GenerateParallax also needs to support the thingy.
        // TODO: Can Parallel these and then load into List sequentially.
        private async Task<ParallaxLayer?> LoadLayer(TomlTable config, ISawmill sawmill)
        {
            var name = "";
            var path = new ResourcePath($"parallax_{name}");
            Image image;
            Texture texture;

            if (_resourceCache.UserData.Exists(path))
            {
                await using var stream = _resourceCache.UserData.Open(path, FileMode.Open);
                image = await Image.LoadAsync(Configuration.Default, stream);
                texture = Texture.LoadFromImage((Image<Rgba32>) image, $"parallax_{name}");
                image.Dispose();
            }
            else
            {
                // TODO: Variable resolution.
                if (config.TryGetValue("texture", out var texturePath))
                {
                    if (!_resourceCache.TryGetResource(texturePath.ToString(), out TextureResource? resource))
                    {
                        sawmill.Error($"Unable to find {texturePath} path for parallax {name}");
                        return null;
                    }

                    texture = resource.Texture;
                }
                else if (config.TryGetValue("generated", out var generator))
                {
                    image = await Task.Run(() =>
                        ParallaxGenerator.GenerateParallax(config, new Size(1920, 1080), sawmill));

                    // Store cache
                    if (!_resourceCache.UserData.Exists(path))
                    {
                        // Store it and CRC so further game starts don't need to regenerate it.
                        await using var stream = _resourceCache.UserData.Create(path);
                        await image.SaveAsPngAsync(stream);
                    }

                    texture = Texture.LoadFromImage((Image<Rgba32>) image, $"parallax_{name}");
                    image.Dispose();
                }
                else
                {
                    sawmill.Error("Unable to generate parallax for {name}");
                    return null;
                }
            }

            var layer = new ParallaxLayer
            {
                Name = name,
                ParallaxTexture = texture,
            };

            return layer;
        }
    }
}
