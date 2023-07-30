using Robust.Shared.Prototypes;

namespace Content.Server.Cargo;

[Prototype("cargoCategory")]
public sealed class CargoCategoryPrototype : IPrototype
{
    [IdDataField] public string ID { get; } = default!;

    /// <summary>
    /// The minimum amount items in this category can sell for.
    /// </summary>
    [DataField("minModifier")]
    public float MinModifier = 0.20f;

    /// <summary>
    /// % Sell price decreases after selling 1 unit (e.g. entity, gas, etc) in this category
    /// </summary>
    [DataField("decreaseRate")]
    public float DecreaseRate = 0.05f;

    // TODO: Add CargoCategoryComponent and for each entity sold decrease price.
}
