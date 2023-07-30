using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Cargo.Components;

[RegisterComponent]
public sealed class CargoCategoryComponent : Component
{
    /// <summary>
    /// Category we correspond to for sell modifiers.
    /// </summary>
    [DataField("category", required: true, customTypeSerializer:typeof(PrototypeIdSerializer<CargoCategoryPrototype>))]
    public string Category = string.Empty;
}
