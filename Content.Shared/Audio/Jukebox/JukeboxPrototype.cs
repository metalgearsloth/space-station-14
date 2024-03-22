using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.Audio.Jukebox;

[Prototype]
public sealed class JukeboxPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = string.Empty;

    /// <summary>
    /// No collection because we don't want random
    /// </summary>
    [DataField(required: true)]
    public SoundPathSpecifier Audio = default!;
}
