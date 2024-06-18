using System.Numerics;
using System.Threading;
using Content.Shared.Procedural;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Procedural;

/// <summary>
/// Template prototype that can be used for <see cref="ProceduralComponent"/> defaults or for dungeons.
/// </summary>
[Prototype]
public sealed partial class ProceduralPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = string.Empty;

    /// <summary>
    /// Generic data to be used across layers, e.g. which walls to use.
    /// This is overwritten by <see cref="ProceduralMetaLayer"/> data.
    /// </summary>
    [DataField]
    public DungeonData Data = new();

    [DataField]
    public List<ProceduralMetaLayer> Layers = new();
}

/// <summary>
/// Wrapper around <see cref="ProceduralPrototype"/> for fixed-area procedural generation.
/// </summary>
[Prototype]
public sealed partial class ProceduralDungeonPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = string.Empty;

    /// <summary>
    /// Fixed bounds to generate the procgen content inside of.
    /// </summary>
    [DataField(required: true)]
    public Vector2i Size;
}

/// <summary>
/// When added to a map will continuously generate procedural data.
/// </summary>
[RegisterComponent]
public sealed partial class ProceduralComponent : Component
{
    /// <summary>
    /// Disables processing entirely.
    /// </summary>
    [DataField]
    public bool Enabled = true;

    /// <summary>
    /// Next time we check for chunks to load / unload.
    /// The actual queue for loading is handled independently.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextUpdate = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Global flags for every layer.
    /// </summary>
    [DataField]
    public ProceduralMetaFlags Flags = ProceduralMetaFlags.None;

    [DataField]
    public ProtoId<ProceduralPrototype>? TemplateProto;

    [DataField]
    public List<ProceduralMetaLayer> Layers = new();

    /// <summary>
    /// Areas to be loaded for this component.
    /// </summary>
    public List<Box2> AreasOfInterest;

    /// <summary>
    /// Cached inverse world-matrix.
    /// </summary>
    public Matrix3x2 InvMatrix;

    /// <summary>
    /// Data for this meta-layer.
    /// </summary>
    [DataField]
    public DungeonData Data = new();
}


/// <summary>
/// Top-level layers for procgen.
/// All of the internal layers for a Meta layer share the same data.
/// External access is done via defining the meta layer as being dependent upon another.
/// </summary>
public sealed class ProceduralMetaLayer()
{
    /// <summary>
    /// Identifier for this meta-layer. Can be referred to by other layers.
    /// </summary>
    [DataField(required: true)]
    public string Id = string.Empty;

    /// <summary>
    /// How large chunks are in this meta-layer.
    /// </summary>
    [DataField]
    public int ChunkSize = 8;

    /// <summary>
    /// Flags for this layer.
    /// </summary>
    [DataField]
    public ProceduralMetaFlags Flags = ProceduralMetaFlags.None;

    [DataField(required: true)]
    public List<IProceduralLayer> Layers = new();

    /// <summary>
    /// <see cref="ProceduralLayerDependency"/>
    /// </summary>
    [DataField]
    public List<ProceduralLayerDependency> Dependencies = new();

    /*
     * Runtime data
     */

    /// <summary>
    /// Don't serialize these because loading happens inside of a single tick.
    /// </summary>
    public Dictionary<Vector2i, CancellationTokenSource> LoadingChunks = new();

    /// <summary>
    /// Chunks that are currently loaded.
    /// </summary>
    [DataField]
    public HashSet<Vector2i> LoadedChunks = new();

    /// <summary>
    /// Chunks that have been modified and need to persist.
    /// </summary>
    [DataField]
    public HashSet<Vector2i> PersistedChunks = new();

    /// <summary>
    /// Chunks that are no longer in range and are pending unload.
    /// </summary>
    public Dictionary<Vector2i, CancellationTokenSource> UnloadingChunks = new();
}

[Flags]
public enum ProceduralMetaFlags : byte
{
    None = 0,

    /// <summary>
    /// The meta layer will always remain loaded.
    /// </summary>
    NoUnload = 1 << 0,
}

[ImplicitDataDefinitionForInheritors]
public partial interface IProceduralLayer
{

}

/// <summary>
/// Which meta-layers this one is dependent upon being finished.
/// </summary>
[DataRecord]
public record struct ProceduralLayerDependency()
{
    [DataField(required: true)]
    public string Target = string.Empty;
}
