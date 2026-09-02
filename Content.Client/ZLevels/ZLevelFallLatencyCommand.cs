using System;
using System.Globalization;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.Client.ZLevels;

/// <summary>
/// Toggles a substantial fixed delay on client-to-server packets for manually inspecting authoritative fall
/// handoffs. Delaying input leaves several ticks of visible XY prediction outstanding before the server can begin
/// and replicate the fall.
/// </summary>
public sealed partial class ZLevelFallLatencyCommand : LocalizedCommands
{
    private const float DefaultLatencySeconds = 0.35f;
    private const float MaximumLatencySeconds = 5f;

    [Dependency] private IConfigurationManager _configuration = default!;

    public override string Command => "zfall_latency";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1)
        {
            shell.WriteError(Help);
            return;
        }

        float latency;
        if (args.Length == 0)
        {
            latency = MathF.Abs(_configuration.GetCVar(CVars.NetFakeLagMin) - DefaultLatencySeconds) < 0.001f
                ? 0f
                : DefaultLatencySeconds;
        }
        else if (args[0].Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            latency = 0f;
        }
        else if (!float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out latency) ||
                 !float.IsFinite(latency) ||
                 latency < 0f ||
                 latency > MaximumLatencySeconds)
        {
            shell.WriteError(Help);
            return;
        }

        _configuration.SetCVar(CVars.NetFakeLagMin, latency);
        _configuration.SetCVar(CVars.NetFakeLagRand, 0f);
        shell.WriteLine(Loc.GetString("cmd-zfall_latency-status", ("latency", latency)));
    }
}
