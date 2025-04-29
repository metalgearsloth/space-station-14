using System.Text.RegularExpressions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Disposal.Tube;

[RegisterComponent, NetworkedComponent]
public sealed partial class DisposalTaggerComponent : Component
{
    [DataField]
    public string Tag = "";

    [DataField]
    public SoundSpecifier ClickSound = new SoundPathSpecifier("/Audio/Machines/machine_switch.ogg");

    public static readonly Regex TagRegex = new("^[a-zA-Z0-9 ]*$", RegexOptions.Compiled);
}

[Serializable, NetSerializable]
public sealed class DisposalTaggerUiActionMessage : BoundUserInterfaceMessage
{
    public readonly DisposalTaggerUiAction Action;
    public readonly string Tag = "";

    public DisposalTaggerUiActionMessage(DisposalTaggerUiAction action, string tag)
    {
        Action = action;

        if (Action == DisposalTaggerUiAction.Ok)
        {
            Tag = tag.Substring(0, Math.Min(tag.Length, 30));
        }
    }
}

[Serializable, NetSerializable]
public enum DisposalTaggerUiAction : byte
{
    Ok
}

[Serializable, NetSerializable]
public enum DisposalTaggerUiKey : byte
{
    Key
}
