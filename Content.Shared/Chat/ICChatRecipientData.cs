namespace Content.Shared.Chat;

public readonly record struct ICChatRecipientData(float Range, bool Observer, bool? HideChatOverride = null)
{
}