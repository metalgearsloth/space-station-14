using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Audio.Jukebox;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class JukeboxComponent : Component
{
    [DataField]
    public ProtoId<JukeboxPrototype>? SelectedProto;

    [DataField]
    public EntityUid? Stream;

    public bool Active => Stream != null;
}

[Serializable, NetSerializable]
public enum JukeboxUi : byte
{
    Key,
}
