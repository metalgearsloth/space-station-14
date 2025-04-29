using System.Text.RegularExpressions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Disposal.Mailing;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DisposalRouterComponent : Component
{
    public static readonly Regex TagRegex = new("^[a-zA-Z0-9, ]*$", RegexOptions.Compiled);

    [DataField, AutoNetworkedField]
    public HashSet<string> Tags = new();

    [DataField]
    public SoundSpecifier ClickSound = new SoundPathSpecifier("/Audio/Machines/machine_switch.ogg");

    /// <summary>
    ///     The angles to connect to.
    /// </summary>
    [DataField] public List<Angle> Degrees = new();
}

[Serializable, NetSerializable]
public sealed class DisposalRouterUiActionMessage : BoundUserInterfaceMessage
{
    public readonly DisposalRouterUiAction Action;
    public readonly string Tags = "";

    public DisposalRouterUiActionMessage(DisposalRouterUiAction action, string tags)
    {
        Action = action;

        if (Action == DisposalRouterUiAction.Ok)
        {
            Tags = tags.Substring(0, Math.Min(tags.Length, 150));
        }
    }
}

[Serializable, NetSerializable]
public enum DisposalRouterUiAction : byte
{
    Ok
}

[Serializable, NetSerializable]
public enum DisposalRouterUiKey : byte
{
    Key
}
