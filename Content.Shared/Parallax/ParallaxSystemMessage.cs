using Robust.Shared.GameObjects;

namespace Content.Shared.Parallax
{
    public sealed class ParallaxSystemMessage : EntityEventArgs
    {
        public string ID { get; }

        public ParallaxSystemMessage(string id)
        {
            ID = id;
        }
    }

    /// <summary>
    ///     Sent from client to server when they want to know what the round's parallax is
    /// </summary>
    public sealed class RequestParallaxMessage : EntityEventArgs { }
}
