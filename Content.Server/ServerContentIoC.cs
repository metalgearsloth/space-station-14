using Content.Server.AI.HTN;
using Content.Server.AI.HTN.Planner;
using Content.Server.Cargo;
using Content.Server.Chat;
using Content.Server.GameObjects.Components.Pathfinding;
using Content.Server.GameObjects.EntitySystems.Pathfinding.Pathfinders;
using Content.Server.GameTicking;
using Content.Server.Interfaces;
using Content.Server.Interfaces.Chat;
using Content.Server.Interfaces.GameTicking;
using Content.Server.Preferences;
using Content.Server.Sandbox;
using Content.Server.Utility;
using Content.Shared.Interfaces;
using Robust.Shared.IoC;

namespace Content.Server
{
    internal static class ServerContentIoC
    {
        public static void Register()
        {
            IoCManager.Register<ISharedNotifyManager, ServerNotifyManager>();
            IoCManager.Register<IServerNotifyManager, ServerNotifyManager>();
            IoCManager.Register<IGameTicker, GameTicker>();
            IoCManager.Register<IChatManager, ChatManager>();
            IoCManager.Register<IMoMMILink, MoMMILink>();
            IoCManager.Register<ISandboxManager, SandboxManager>();
            IoCManager.Register<IGalacticBankManager, GalacticBankManager>();
            IoCManager.Register<ICargoOrderDataManager, CargoOrderDataManager>();
            IoCManager.Register<IModuleManager, ServerModuleManager>();
            IoCManager.Register<IServerPreferencesManager, ServerPreferencesManager>();
            IoCManager.Register<IPathfinder, AStarPathfinder>();
            IoCManager.Register<IPlanner, HtnPlanner>();
        }
    }
}
