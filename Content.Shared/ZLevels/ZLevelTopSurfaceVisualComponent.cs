namespace Content.Shared.ZLevels;

/// <summary>
/// Marks an authored high-ground provider whose walkable top must be presented independently from its side sprite.
/// The renderer uses the provider's actual support shape and height, so this visual is also the authoritative picking
/// footprint rather than a decorative approximation.
/// </summary>
[RegisterComponent]
public sealed partial class ZLevelTopSurfaceVisualComponent : Component
{
    [DataField]
    public Color FillColor = new(0.20f, 0.65f, 0.78f, 0.24f);

    [DataField]
    public Color EdgeColor = new(0.58f, 0.92f, 1f, 0.86f);
}
