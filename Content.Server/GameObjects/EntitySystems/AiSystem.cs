using System;
using System.Collections.Generic;
using Content.Server.AI.HTN.Agents.Group;
using Content.Server.AI.HTN.Agents.Individual;
using Content.Server.GameObjects.Components.Movement;
using Content.Server.Interfaces.GameObjects.Components.Movement;
using Robust.Server.AI;
using Robust.Server.Interfaces.Console;
using Robust.Server.Interfaces.Player;
using Robust.Server.Interfaces.Timing;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Reflection;
using Robust.Shared.IoC;

namespace Content.Server.GameObjects.EntitySystems
{
    internal class AiSystem : EntitySystem
    {
#pragma warning disable 649
        [Dependency] private readonly IPauseManager _pauseManager;
        [Dependency] private readonly IDynamicTypeFactory _typeFactory;
        [Dependency] private readonly IReflectionManager _reflectionManager;
#pragma warning restore 649

        private readonly Dictionary<string, Type> _processorTypes = new Dictionary<string, Type>();
        private readonly List<GroupAiManager> _aiManagers = new List<GroupAiManager>();

        public event Action<AiAgent> RequestAiManager;

        /// <inheritdoc />
        public override void Initialize()
        {
            base.Initialize();

            // register entity query
            EntityQuery = new TypeEntityQuery(typeof(AiControllerComponent));

            var processors = _reflectionManager.GetAllChildren<AiLogicProcessor>();
            foreach (var processor in processors)
            {
                var att = (AiLogicProcessorAttribute)Attribute.GetCustomAttribute(processor, typeof(AiLogicProcessorAttribute));
                if (att != null)
                {
                    _processorTypes.Add(att.SerializeName, processor);
                }
            }

            SetupAiManagers();
        }

        private void SetupAiManagers()
        {
            _aiManagers.Add(new CivilianAiGroupManager());

            foreach (var manager in _aiManagers)
            {
                manager.Setup();
            }
        }

        /// <inheritdoc />
        public override void Update(float frameTime)
        {

            var entities = EntityManager.GetEntities(EntityQuery);
            foreach (var entity in entities)
            {
                if (_pauseManager.IsEntityPaused(entity))
                {
                    continue;
                }

                entity.TryGetComponent(out AiControllerComponent aiComp);
                ProcessorInitialize(aiComp);

                var processor = aiComp.Processor;

                if (processor.AiManager == null)
                {
                    RequestAiManager?.Invoke(processor);
                }

                processor.Update(frameTime);
            }
        }

        /// <summary>
        /// Will start up the controller's processor if not already done so
        /// </summary>
        /// <param name="controller"></param>
        public void ProcessorInitialize(AiControllerComponent controller)
        {
            if (controller.Processor != null) return;
            controller.Processor = CreateProcessor(controller.LogicName);
            controller.Processor.SelfEntity = controller.Owner;
            controller.Processor.VisionRadius = controller.VisionRadius;
            controller.Processor.Setup();
        }

        private AiAgent CreateProcessor(string name)
        {
            if (_processorTypes.TryGetValue(name, out var type))
            {
                return (AiAgent)_typeFactory.CreateInstance(type);
            }

            // processor needs to inherit AiLogicProcessor, and needs an AiLogicProcessorAttribute to define the YAML name
            throw new ArgumentException($"Processor type {name} could not be found.", nameof(name));
        }

        private class AddAiCommand : IClientCommand
        {
            public string Command => "addai";
            public string Description => "Add an ai component with a given processor to an entity.";
            public string Help => "addai <processorId> <entityId>";
            public void Execute(IConsoleShell shell, IPlayerSession player, string[] args)
            {
                if(args.Length != 2)
                {
                    shell.SendText(player, "Wrong number of args.");
                    return;
                }

                var processorId = args[0];
                var entId = new EntityUid(int.Parse(args[1]));
                var ent = IoCManager.Resolve<IEntityManager>().GetEntity(entId);

                if (ent.HasComponent<AiControllerComponent>())
                {
                    shell.SendText(player, "Entity already has an AI component.");
                    return;
                }

                if (ent.HasComponent<IMoverComponent>())
                {
                    ent.RemoveComponent<IMoverComponent>();
                }

                var comp = ent.AddComponent<AiControllerComponent>();
                comp.LogicName = processorId;
                shell.SendText(player, "AI component added.");
            }
        }
    }
}
