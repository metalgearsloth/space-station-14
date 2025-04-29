using Content.Shared.Damage;
using Content.Shared.Disposal.Unit;
using Robust.Shared.Audio;
using Robust.Shared.Containers;

namespace Content.Shared.Disposal.Tube;

[RegisterComponent]
[Access(typeof(SharedDisposalTubeSystem), typeof(SharedDisposableSystem))]
public sealed partial class DisposalTubeComponent : Component
{
    [DataField]
    public string ContainerId = "DisposalTube";

    [DataField]
    public SoundSpecifier ClangSound = new SoundPathSpecifier("/Audio/Effects/clang.ogg", AudioParams.Default.WithVolume(-5f));

    /// <summary>
    ///     Container of entities that are currently inside this tube
    /// </summary>
    [ViewVariables]
    public Container Contents = default!;

    /// <summary>
    /// Damage dealt to containing entities on every turn
    /// </summary>
    [DataField]
    public DamageSpecifier DamageOnTurn = new()
    {
        DamageDict = new()
        {
            { "Blunt", 0.0 },
        }
    };
}
