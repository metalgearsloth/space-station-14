using Robust.Shared.Prototypes;

namespace Content.Shared.Parallax.Biomes;

/// <summary>
/// A preset group of biome layers to be used for a <see cref="BiomeComponent"/>
/// </summary>
[Prototype]
public sealed partial class BiomeTemplatePrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;

    [DataField]
    public Dictionary<string, BiomeMetaLayer> Layers = new();
}
