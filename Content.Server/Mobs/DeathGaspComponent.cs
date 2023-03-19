using Content.Shared.Chat.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Mobs;

/// <summary>
/// Lets out an emote when dead
/// </summary>
[RegisterComponent]
public sealed class DeathGaspComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField("proto", customTypeSerializer:typeof(PrototypeIdSerializer<EmotePrototype>))]
    public string Prototype = "DeathGasp";
}
