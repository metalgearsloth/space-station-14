namespace Content.Shared.Chat;

public sealed class TransformSpeakerNameEvent : EntityEventArgs
{
    public EntityUid Sender;
    public string Name;

    public TransformSpeakerNameEvent(EntityUid sender, string name)
    {
        Sender = sender;
        Name = name;
    }
}