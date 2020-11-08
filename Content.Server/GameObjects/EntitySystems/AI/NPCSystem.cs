#nullable enable
using System;
using System.Collections.Generic;
using Content.Server.AI.Utility;
using Content.Server.AI.Utility.Actions;
using Content.Server.GameObjects.Components.Movement;
using Content.Shared;
using Content.Shared.GameObjects.Components.Movement;
using JetBrains.Annotations;
using Robust.Server.AI;
using Robust.Server.Interfaces.Console;
using Robust.Server.Interfaces.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.Configuration;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Reflection;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.GameObjects.EntitySystems.AI
{
    [UsedImplicitly]
    internal class NPCSystem : EntitySystem
    {
        [Dependency] private readonly IConfigurationManager _configurationManager = default!;
        [Dependency] private readonly IDynamicTypeFactory _typeFactory = default!;
        [Dependency] private readonly IReflectionManager _reflectionManager = default!;

        private readonly Dictionary<string, Type> _processorTypes = new Dictionary<string, Type>();

        /// <summary>
        ///     To avoid iterating over dead AI continuously they can wake and sleep themselves when necessary.
        /// </summary>
        private readonly HashSet<NPCComponent> _awakeAi = new HashSet<NPCComponent>();

        // To avoid modifying awakeAi while iterating over it.
        private readonly List<SleepAiMessage> _queuedSleepMessages = new List<SleepAiMessage>();

        public bool IsAwake(NPCComponent comp) => _awakeAi.Contains(comp);

        private Dictionary<string, List<IAiUtility>> _behaviorSets = new Dictionary<string, List<IAiUtility>>();

        /// <inheritdoc />
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<SleepAiMessage>(HandleAiSleep);

            var protoManager = IoCManager.Resolve<IPrototypeManager>();
            var reflectionManager = IoCManager.Resolve<IReflectionManager>();
            var typeFactory = IoCManager.Resolve<IDynamicTypeFactory>();

            foreach (var proto in protoManager.EnumeratePrototypes<BehaviorSet>())
            {
                var actions = new List<IAiUtility>();

                foreach (var action in GetActions(proto, protoManager, reflectionManager, typeFactory))
                {
                    actions.Add(action);
                }

                _behaviorSets[proto.ID] = actions;
            }
        }

        private IEnumerable<IAiUtility> GetActions(BehaviorSet behaviorSet, IPrototypeManager? prototypeManager = null, IReflectionManager? reflectionManager = null, IDynamicTypeFactory? typeFactory = null)
        {
            prototypeManager ??= IoCManager.Resolve<IPrototypeManager>();
            reflectionManager ??= IoCManager.Resolve<IReflectionManager>();
            typeFactory ??= IoCManager.Resolve<IDynamicTypeFactory>();

            if (behaviorSet.Parent != null)
            {
                var parent = IoCManager.Resolve<IPrototypeManager>().Index<BehaviorSet>(behaviorSet.Parent);
                foreach (var action in GetActions(parent, prototypeManager, reflectionManager, typeFactory))
                {
                    yield return action;
                }
            }

            foreach (var action in behaviorSet.Actions)
            {
                var type = reflectionManager.LooseGetType(action);
                if (type == null || !typeof(IAiUtility).IsAssignableFrom(type))
                {
                    Logger.Error($"Invalid type {action} for BehaviorSet {behaviorSet.ID}");
                    continue;
                }

                yield return (IAiUtility) typeFactory.CreateInstance(type);
            }
        }

        /// <inheritdoc />
        public override void Update(float frameTime)
        {
            var cvarMaxUpdates = _configurationManager.GetCVar(CCVars.AIMaxUpdates);
            if (cvarMaxUpdates <= 0)
                return;

            foreach (var message in _queuedSleepMessages)
            {
                switch (message.Sleep)
                {
                    case true:
                        if (_awakeAi.Count == cvarMaxUpdates && _awakeAi.Contains(message.Component))
                        {
                            Logger.Warning($"Under AI limit again: {_awakeAi.Count - 1} / {cvarMaxUpdates}");
                        }
                        _awakeAi.Remove(message.Component);
                        break;
                    case false:
                        _awakeAi.Add(message.Component);

                        if (_awakeAi.Count > cvarMaxUpdates)
                        {
                            Logger.Warning($"AI limit exceeded: {_awakeAi.Count} / {cvarMaxUpdates}");
                        }
                        break;
                }
            }

            _queuedSleepMessages.Clear();
            var toRemove = new List<NPCComponent>();
            var maxUpdates = Math.Min(_awakeAi.Count, cvarMaxUpdates);
            var count = 0;

            foreach (var comp in _awakeAi)
            {
                if (count >= maxUpdates)
                {
                    break;
                }

                if (comp.Deleted)
                {
                    toRemove.Add(comp);
                    continue;
                }

                comp.Update(frameTime);
                count++;
            }

            foreach (var processor in toRemove)
            {
                _awakeAi.Remove(processor);
            }
        }

        private void HandleAiSleep(SleepAiMessage message)
        {
            _queuedSleepMessages.Add(message);
        }

        public List<IAiUtility> GetBehaviorActions(string behaviorSet)
        {
            return new List<IAiUtility>(_behaviorSets[behaviorSet]);
        }

        private class AddAiCommand : IClientCommand
        {
            public string Command => "addai";
            public string Description => "Add an ai component with a given processor to an entity.";
            public string Help => "Usage: addai <processorId> <entityId>"
                                + "\n    processorId: Class that inherits AiLogicProcessor and has an AiLogicProcessor attribute."
                                + "\n    entityID: Uid of entity to add the AiControllerComponent to. Open its VV menu to find this.";

            public void Execute(IConsoleShell shell, IPlayerSession? player, string[] args)
            {
                if(args.Length != 2)
                {
                    shell.SendText(player, "Wrong number of args.");
                    return;
                }

                var processorId = args[0];
                var entId = new EntityUid(int.Parse(args[1]));
                var ent = IoCManager.Resolve<IEntityManager>().GetEntity(entId);
                var aiSystem = Get<NPCSystem>();

                if (!aiSystem.ProcessorTypeExists(processorId))
                {
                    shell.SendText(player, "Invalid processor type. Processor must inherit AiLogicProcessor and have an AiLogicProcessor attribute.");
                    return;
                }
                if (ent.HasComponent<NPCComponent>())
                {
                    shell.SendText(player, "Entity already has an AI component.");
                    return;
                }

                if (ent.HasComponent<IMoverComponent>())
                {
                    ent.RemoveComponent<IMoverComponent>();
                }

                var comp = ent.AddComponent<NPCComponent>();
                comp.LogicName = processorId;
                shell.SendText(player, "AI component added.");
            }
        }
    }
}
