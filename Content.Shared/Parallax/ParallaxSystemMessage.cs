using Robust.Shared.GameObjects;

namespace Content.Shared.Parallax
{
    public sealed class ParallaxSystemMessage : EntitySystemMessage
    {
        public string ID { get; }

        public ParallaxSystemMessage(string id)
        {
            ID = id;
        }
    }
}
