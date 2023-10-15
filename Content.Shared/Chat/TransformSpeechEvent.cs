namespace Content.Shared.Chat;

/// <summary>
///     Raised broadcast in order to transform speech.transmit
/// </summary>
public sealed class TransformSpeechEvent : EntityEventArgs
{
    public EntityUid Sender;
    public string Message;

    public TransformSpeechEvent(EntityUid sender, string message)
    {
        Sender = sender;
        Message = message;
    }
}