#nullable enable
using Content.Server.Administration;
using Content.Shared.GameObjects.Components;
using Content.Server.GameObjects.Components;
using Content.Server.GameObjects.Components.Items.Storage;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Server.Commands.GameTicking
{
    [AdminCommand(AdminFlags.Mapping)]
    class FixRotationsCommand : IConsoleCommand
    {
        // ReSharper disable once StringLiteralTypo
        public string Command => "fixrotations";
        public string Description => "Sets the rotation of all occluders, low walls and windows to the specified angle (in degrees).";
        public string Help => $"Usage: {Command} <gridId> <angle> | {Command} <angle>";

        public void Execute(IConsoleShell shell, string argsOther, string[] args)
        {
            var player = shell.Player as IPlayerSession;

            GridId gridId;
            int angleDegrees;

            switch (args.Length)
            {
                case 1:
                    if (player?.AttachedEntity == null)
                    {
                        shell.WriteLine("Only a player can run this command.");
                        return;
                    }

                    if (!int.TryParse(args[0], out angleDegrees))
                    {
                        shell.WriteLine($"{args[0]} is not a valid angle.");
                        return;
                    }

                    gridId = player.AttachedEntity.Transform.GridID;
                    break;
                case 2:
                    if (!int.TryParse(args[0], out var id))
                    {
                        shell.WriteLine($"{args[0]} is not a valid integer.");
                        return;
                    }

                    if (!int.TryParse(args[1], out angleDegrees))
                    {
                        shell.WriteLine($"{args[1]} is not a valid angle.");
                        return;
                    }

                    gridId = new GridId(id);
                    break;
                default:
                    shell.WriteLine(Help);
                    return;
            }

            var angle = Angle.FromDegrees(angleDegrees);
            var mapManager = IoCManager.Resolve<IMapManager>();
            if (!mapManager.TryGetGrid(gridId, out var grid))
            {
                shell.WriteLine($"No grid exists with id {gridId}");
                return;
            }

            var entityManager = IoCManager.Resolve<IEntityManager>();
            if (!entityManager.TryGetEntity(grid.GridEntityId, out var gridEntity))
            {
                shell.WriteLine($"Grid {gridId} doesn't have an associated grid entity.");
                return;
            }

            var changed = 0;
            foreach (var childUid in gridEntity.Transform.ChildEntityUids)
            {
                if (!entityManager.TryGetEntity(childUid, out var childEntity))
                {
                    continue;
                }

                var valid = false;

                if (childEntity.HasComponent<IMapGridComponent>() || childEntity.HasComponent<MapComponent>())
                {
                    continue;
                }

                valid |= childEntity.HasComponent<OccluderComponent>();
                valid |= childEntity.HasComponent<SharedCanBuildWindowOnTopComponent>();
                valid |= childEntity.HasComponent<WindowComponent>();
                valid |= childEntity.HasComponent<ServerStorageComponent>();

                if (!valid)
                {
                    continue;
                }

                if (childEntity.Transform.LocalRotation != angle)
                {
                    childEntity.Transform.LocalRotation = angle;
                    var localPos = childEntity.Transform.LocalPosition;
                    DebugTools.Assert(!float.IsNaN(localPos.X) && !float.IsNaN(localPos.Y));
                    changed++;
                }
            }

            shell.WriteLine($"Changed {changed} entities. If things seem wrong, reconnect.");
        }
    }
}
