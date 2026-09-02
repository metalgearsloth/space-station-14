using Robust.Shared.Analyzers;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.ZLevels;
[RegisterComponent]
[Access(typeof(ZLevelPortalSystem))]
public sealed partial class ZLevelPortalComponent : Component
{
    /// <summary>
    /// The adjacent level reached from this endpoint. Must be either -1 or +1.
    /// </summary>
    [DataField(required: true)]
    public int DestinationOffset;

    /// <summary>
    /// Prototype used when automatically creating the endpoint on the adjacent level.
    /// </summary>
    [DataField]
    public EntProtoId<ZLevelPortalComponent>? CounterpartPrototype;

    /// <summary>
    /// Explicitly linked endpoint on the adjacent level.
    /// </summary>
    [ViewVariables]
    public EntityUid? PairedEndpoint;

    /// <summary>
    /// Endpoint that generated this entity, if any.
    /// </summary>
    [ViewVariables]
    public EntityUid? GeneratedBy;
}
