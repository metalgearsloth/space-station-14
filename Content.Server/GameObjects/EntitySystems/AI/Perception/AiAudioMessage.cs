using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.GameObjects.EntitySystems.AI.Perception
{
    public class AiAudioMessage : EntitySystemMessage
    {
        public EntityUid Source { get; }
        public MapCoordinates MapCoordinates { get; }

        public AiAudioMessage(EntityUid source, MapCoordinates mapCoordinates)
        {
            Source = source;
            MapCoordinates = mapCoordinates;
        }
    }
}