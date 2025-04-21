using Content.Shared.Parallax.Biomes;
using Robust.Shared.Noise;
using Robust.Shared.Prototypes;

namespace Content.Shared.Procedural.DungeonGenerators;

/// <summary>
/// Uses a noise sample to determine what <see cref="DungeonConfigPrototype"/> to use at the specified tile.
/// </summary>
public sealed partial class SampleMetaDunGen : IDunGenLayer
{
    [DataField]
    public FastNoiseLite Noise { get; private set; } = new(0);

    /// <inheritdoc/>
    [DataField]
    public float Threshold { get; private set; } = -1f;

    /// <inheritdoc/>
    [DataField]
    public bool Invert { get; private set; }

    [DataField]
    public ProtoId<DungeonConfigPrototype> Template = string.Empty;
}
