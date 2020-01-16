﻿using Content.Server.Interfaces.GameObjects.Components.Movement;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;
using Robust.Shared.ViewVariables;
using AiAgent = Content.Server.AI.HTN.Agents.Individual.AiAgent;

namespace Content.Server.GameObjects.Components.Movement
{
    [RegisterComponent, ComponentReference(typeof(IMoverComponent))]
    public class AiControllerComponent : Component, IMoverComponent
    {
        private string _logicName;
        private float _visionRadius;

        public override string Name => "AiController";

        [ViewVariables(VVAccess.ReadWrite)]
        public string LogicName
        {
            get => _logicName;
            set
            {
                _logicName = value;
                Processor = null;
            }
        }

        public AiAgent Processor { get; set; }

        [ViewVariables(VVAccess.ReadWrite)]
        public float VisionRadius
        {
            get => _visionRadius;
            set => _visionRadius = value;
        }

        /// <inheritdoc />
        public override void Initialize()
        {
            base.Initialize();

            // This component requires a physics component.
            if (!Owner.HasComponent<PhysicsComponent>())
                Owner.AddComponent<PhysicsComponent>();
        }

        /// <inheritdoc />
        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);

            serializer.DataField(ref _logicName, "logic", null);
            serializer.DataField(ref _visionRadius, "vision", 8.0f);
        }

        /// <summary>
        ///     Movement speed (m/s) that the entity walks.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public float WalkMoveSpeed { get; set; } = 4.0f;

        /// <summary>
        ///     Movement speed (m/s) that the entity sprints.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public float SprintMoveSpeed { get; set; } = 7.0f;

        /// <summary>
        ///     Is the entity Sprinting (running)?
        /// </summary>
        [ViewVariables]
        public bool Sprinting { get; set; } = true;

        /// <summary>
        ///     Calculated linear velocity direction of the entity.
        /// </summary>
        [ViewVariables]
        public Vector2 VelocityDir { get; set; }

        public GridCoordinates LastPosition { get; set; }

        [ViewVariables(VVAccess.ReadWrite)]
        public float StepSoundDistance { get; set; }

        public void SetVelocityDirection(Direction direction, bool enabled) { }
    }
}
