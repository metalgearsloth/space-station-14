using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Console;

namespace Content.Client.ZLevels;

public sealed partial class ZLevelSupportDebugCommand : LocalizedCommands
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IResourceCache _resources = default!;
    [Dependency] private IEntityManager _entities = default!;

    public override string Command => "showzsupport";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var enabled = !_overlays.RemoveOverlay<ZLevelSupportDebugOverlay>();
        _overlays.RemoveOverlay<ZLevelSurfaceOverlay>();
        if (enabled)
        {
            _overlays.AddOverlay(new ZLevelSupportDebugOverlay(_entities, _resources));
            _overlays.AddOverlay(new ZLevelSurfaceOverlay(_entities, _players));
        }

        shell.WriteLine(Loc.GetString("cmd-showzsupport-status", ("status", enabled)));
    }
}
