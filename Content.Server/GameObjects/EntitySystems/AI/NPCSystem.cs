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
    public sealed class NPCSystem : EntitySystem
    {
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly IReflectionManager _reflectionManager = default!;
        [Dependency] private readonly IDynamicTypeFactory _typeFactory = default!;
        [Dependency] private readonly IConfigurationManager _configurationManager = default!;

        /// <summary>
        ///     To avoid iterating over dead AI continuously they can wake and sleep themselves when necessary.
        /// </summary>
        private readonly HashSet<NPCComponent> _awakeAi = new HashSet<NPCComponent>();

        // To avoid modifying awakeAi while iterating over it.
        private readonly List<SleepAiMessage> _queuedSleepMessages = new List<SleepAiMessage>();

        public bool IsAwake(NPCComponent comp) => _awakeAi.Contains(comp);

        private readonly Dictionary<string, List<IAiUtility>> _behaviorSets = new Dictionary<string, List<IAiUtility>>();

        /// <inheritdoc />
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<SleepAiMessage>(HandleAiSleep);

            foreach (var proto in _prototypeManager.EnumeratePrototypes<BehaviorSetPrototype>())
            {
                var actions = new List<IAiUtility>();

                foreach (var action in GetActions(proto))
                {
                    actions.Add(action);
                }

                _behaviorSets[proto.ID] = actions;
            }
        }

        private IEnumerable<IAiUtility> GetActions(BehaviorSetPrototype behaviorSetPrototype)
        {
            if (behaviorSetPrototype.Parent != null)
            {
                var parent = _prototypeManager.Index<BehaviorSetPrototype>(behaviorSetPrototype.Parent);
                foreach (var action in GetActions(parent))
                {
                    yield return action;
                }
            }

            foreach (var action in behaviorSetPrototype.Actions)
            {
                var type = _reflectionManager.LooseGetType(action);
                if (type == null || !typeof(IAiUtility).IsAssignableFrom(type))
                {
                    Logger.Error($"Invalid type {action} for BehaviorSet {behaviorSetPrototype.ID}");
                    continue;
                }

                if (type.GetConstructor(Type.EmptyTypes) == null)
                {
                    Logger.Error($"No parameterless constructor for NPC action {type}");
                    continue;
                }

                yield return (IAiUtility) _typeFactory.CreateInstance(type);
            }
        }

        public IEnumerable<string> GetBehaviorSets(string profile)
        {
            if (!_prototypeManager.TryIndex<NPCProfilePrototype>(profile, out var npcProfile))
            {
                Logger.Error($"Unable to find NPCProfile {profile}");
                yield break;
            }

            if (npcProfile.Parent != null)
            {
                foreach (var bSet in GetBehaviorSets(npcProfile.Parent))
                {
                    yield return bSet;
                }
            }

            foreach (var bSet in npcProfile.BehaviorSets)
            {
                if (!_prototypeManager.HasIndex<BehaviorSetPrototype>(bSet))
                {
                    Logger.Error($"Unable to find NPC BehaviorSet {bSet} in {npcProfile.ID}");
                    continue;
                }

                yield return bSet;
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
    }
}
