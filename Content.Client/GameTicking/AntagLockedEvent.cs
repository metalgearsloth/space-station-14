namespace Content.Client.GameTicking;

/// <summary>
/// Raised by HumanoidProfileEditor to determine if an antag is locked due to external conditions.
/// </summary>
[ByRefEvent]
public record struct AntagLockedEvent(string Reason, bool Handled);
