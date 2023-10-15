using Robust.Shared.Players;

namespace Content.Shared.Chat;

/// <summary>
///     This event is raised before chat messages are sent out to clients. This enables some systems to send the chat
///     messages to otherwise out-of view entities (e.g. for multiple viewports from cameras).
/// </summary>
public record ExpandICChatRecipientstEvent(EntityUid Source, float VoiceRange, Dictionary<ICommonSession, ICChatRecipientData> Recipients)
{
}
