namespace Content.Client.ZLevels;

/// <summary>
/// Stable renderer-order key for overlapping projected z-level hits.
/// </summary>
public readonly record struct ZLevelProjectedPickKey(
    EntityUid Entity,
    float AbsoluteZ,
    int DrawDepth,
    uint RenderOrder,
    float Bottom);

public static class ZLevelProjectedPicking
{
    /// <summary>
    /// Orders top-most first. Absolute height precedes normal 2-D renderer ordering and UID is the final stable tie.
    /// </summary>
    public static int Compare(in ZLevelProjectedPickKey x, in ZLevelProjectedPickKey y)
    {
        var comparison = y.AbsoluteZ.CompareTo(x.AbsoluteZ);
        if (comparison != 0)
            return comparison;

        comparison = y.DrawDepth.CompareTo(x.DrawDepth);
        if (comparison != 0)
            return comparison;

        comparison = y.RenderOrder.CompareTo(x.RenderOrder);
        if (comparison != 0)
            return comparison;

        comparison = -y.Bottom.CompareTo(x.Bottom);
        if (comparison != 0)
            return comparison;

        return y.Entity.CompareTo(x.Entity);
    }
}
