using Content.Client.ZLevels;
using Robust.Client.Graphics;
using Robust.Shared.Console;

namespace Content.Client.ZLevels.Commands;

public sealed partial class ShowZThrowDebugCommand : LocalizedCommands
{
    [Dependency] private IOverlayManager _overlay = default!;

    public override string Command => "showzthrowdebug";

    public override string Description => "Toggles the z-level throw debug overlay.";

    public override string Help => "Usage: showzthrowdebug";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_overlay.HasOverlay<ZThrowDebugOverlay>())
        {
            _overlay.RemoveOverlay<ZThrowDebugOverlay>();
            shell.WriteLine("Disabled z-level throw debug overlay.");
            return;
        }

        _overlay.AddOverlay(new ZThrowDebugOverlay());
        shell.WriteLine("Enabled z-level throw debug overlay.");
    }
}
