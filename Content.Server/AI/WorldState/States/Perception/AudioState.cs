using System.Collections.Generic;
using System.Linq;
using Content.Server.GameObjects.EntitySystems.AI.Perception;
using Robust.Shared.GameObjects.Systems;

namespace Content.Server.AI.WorldState.States.Perception
{
    public sealed class AudioState : CachedStateData<List<AiAudioMessage>>
    {
        public override string Name => "Audio";
        
        protected override List<AiAudioMessage> GetTrueValue()
        {
            return EntitySystem.Get<AiAudioPerceptionSystem>().DequeueMessages(Owner).ToList();
        }
    }
}