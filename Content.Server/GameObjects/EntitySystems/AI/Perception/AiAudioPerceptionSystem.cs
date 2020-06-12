using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.GameObjects.Components.Movement;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;

namespace Content.Server.GameObjects.EntitySystems.AI.Perception
{
    /// <summary>
    /// Handles AI Audio events
    /// </summary>
    public sealed class AiAudioPerceptionSystem : EntitySystem
    {
        private AiSystem _aiSystem;
        private Queue<AiAudioMessage> _queuedAudio = new Queue<AiAudioMessage>();
        // TODO: Add Event when an entity gets an AiController component added
        private Dictionary<IEntity, Queue<AiAudioMessage>> _messages = new Dictionary<IEntity, Queue<AiAudioMessage>>();
        
        public override void Initialize()
        {
            base.Initialize();
            _aiSystem = Get<AiSystem>();
            SubscribeLocalEvent<AiAudioMessage>(QueueAiAudioMessage);
        }

        /// <summary>
        /// We'll queue up an AI's events until it's ready for them
        /// The AI's probably caching these and getting them infrequently
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public IEnumerable<AiAudioMessage> DequeueMessages(IEntity entity)
        {
            var entityMessages = _messages[entity];
            
            foreach (var message in entityMessages)
            {
                yield return message;
            }
            
            entityMessages.Clear();
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            HandleAiAudioMessages();
        }

        private void QueueAiAudioMessage(AiAudioMessage message)
        {
            _queuedAudio.Enqueue(message);
        }
        
        // If this gets expensive could JobQueue it to timeslice
        private void HandleAiAudioMessages()
        {
            var controllers = _aiSystem.GetControllers().ToList();
            if (controllers.Count == 0)
            {
                _queuedAudio.Clear();
                return;
            }
            
            // Rate limit it in case it blows the eff up
            // 128 total messages handled (across all controllers) given its n x m
            var messages = new List<AiAudioMessage>();
            for (var i = 0; i < Math.Min(128 / controllers.Count * _queuedAudio.Count + 1, _queuedAudio.Count); i++)
            {
                messages.Add(_queuedAudio.Dequeue());
            }

            // Looped like this so we can add multiple messages before checking the size of the queue
            foreach (var controller in controllers)
            {
                var controllerMessages = _messages[controller.Owner];
                
                foreach (var message in messages)
                {
                    if (message.MapCoordinates.MapId != controller.Owner.Transform.MapID)
                    {
                        continue;
                    }
                
                    var distance = (message.MapCoordinates.Position - controller.Owner.Transform.MapPosition.Position).Length;
                    if (controller.AudioRadius <= distance)
                    {
                        controllerMessages.Enqueue(message);
                    }
                }

                // If we have too many queued then we'll just clear them (AI could've stopped thinking or something).
                for (var i = 0; i < Math.Max(0, controllerMessages.Count - 20); i++)
                {
                    controllerMessages.Dequeue();
                }
            }
        }
    }
}