using System.Numerics;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Gateway;

/// <summary>
/// Stores data about a gateway destination.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct GatewayDestination(int Seed, ProtoId<BiomeTemplatePrototype> Biome, Vector2 Offset);

// TODO: need station gateway component to store these options
// TODO: UI
// TODO: offset up to say 256 / 256 away
// TODO: Need some area limiter, needs an overlay and stuff.
