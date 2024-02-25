using Content.Server.Humanoid;
using Content.Shared.Humanoid;
using Content.Shared.Preferences;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Humanoid;

[TestFixture]
public sealed class HumanoidTest
{
    [Test]
    public async Task ValidationTest()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var humanoid = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<HumanoidAppearanceSystem>();

        var badProfile = new HumanoidCharacterProfile();
        badProfile = badProfile.WithSpecies("ABC");

        Assert.That(badProfile.Species, Is.EqualTo("ABC"));

        humanoid.EnsureValid(ref badProfile);
        Assert.That(badProfile.Species, Is.EqualTo(SharedHumanoidAppearanceSystem.DefaultSpecies));

        await pair.CleanReturnAsync();
    }
}
