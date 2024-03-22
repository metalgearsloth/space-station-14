using Robust.Shared.Prototypes;

namespace Content.Client.Audio.Jukebox;

public sealed class JukeboxBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;

    private JukeboxWindow? _window = null;

    public JukeboxBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _protoManager.PrototypesReloaded += OnReload;

        _window = new JukeboxWindow(_entManager);
        _window.BuildList(Owner, _protoManager);
        _window.OpenCentered();

        _window.OnClose += Close;
    }

    private void OnReload(PrototypesReloadedEventArgs obj)
    {
        _window?.BuildList(Owner, _protoManager);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        _protoManager.PrototypesReloaded -= OnReload;
    }
}
