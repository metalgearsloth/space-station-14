#nullable enable
using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests.ZLevels;

[TestFixture]
[TestOf(typeof(ZLevelPhysicsSystem))]
public sealed class ZLevelContainerRegressionTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    [Test]
    public async Task LinkingAdjacentMapDoesNotMoveHumanoidOrgansHandOrStorageContents()
    {
        EntityUid backpack = default;
        EntityUid storedItem = default;
        Dictionary<EntityUid, ContainedSnapshot> before = new();

        await Server.WaitPost(() =>
        {
            var storage = SEntMan.System<SharedStorageSystem>();
            backpack = SEntMan.SpawnEntity("ClothingBackpack", SEntMan.GetCoordinates(PlayerCoords));
            storedItem = SEntMan.SpawnEntity("Pen", SEntMan.GetCoordinates(PlayerCoords));

            Assert.That(storage.Insert(
                backpack,
                storedItem,
                out var stacked,
                playSound: false,
                stackAutomatically: false),
                Is.True);
            Assert.That(stacked, Is.Null);
            Assert.That(Hands?.ActiveHandId, Is.Not.Null);
            Assert.That(HandSys.TryPickup(
                SPlayer,
                backpack,
                Hands!.ActiveHandId!,
                false,
                false,
                false,
                Hands),
                Is.True);

            before = SnapshotContainedDescendants(SPlayer);
            var organCount = before.Values.Count(snapshot => snapshot.Prototype.StartsWith("OrganHuman"));
            Assert.Multiple(() =>
            {
                Assert.That(organCount, Is.GreaterThan(0), "the regression must exercise real humanoid organs");
                Assert.That(before, Contains.Key(backpack), "the regression must exercise a held item");
                Assert.That(before, Contains.Key(storedItem), "the regression must exercise nested storage contents");
                Assert.That(before[backpack].ContainerOwner, Is.EqualTo(SPlayer));
                Assert.That(before[storedItem].ContainerOwner, Is.EqualTo(backpack));
            });
        });

        await Server.WaitPost(() =>
        {
            var upperMap = MapSystem.CreateMap(out _);
            Assert.That(
                SEntMan.System<ZLevelSystem>().TryCreateMapNetwork([MapData.MapUid, upperMap], out _),
                Is.True);
        });

        await Pair.RunTicksSync(180);

        await Server.WaitAssertion(() =>
        {
            var after = SnapshotContainedDescendants(SPlayer);
            var zPhysics = SEntMan.System<ZLevelPhysicsSystem>();
            Assert.That(after.Keys, Is.EquivalentTo(before.Keys),
                "linking a z-map must not add, remove, drop, or expose contained descendants");

            foreach (var (uid, expected) in before)
            {
                Assert.That(SEntMan.EntityExists(uid), Is.True, $"contained entity {uid} was deleted");
                Assert.That(after.TryGetValue(uid, out var actual), Is.True,
                    $"contained entity {uid} was dropped into world space");
                Assert.That(actual, Is.EqualTo(expected),
                    $"container membership or transform changed for {uid} ({expected.Prototype})");
                Assert.That(zPhysics.ActiveBodies, Does.Not.Contain(uid),
                    $"contained entity {uid} independently entered vertical simulation");
            }

            Assert.Multiple(() =>
            {
                Assert.That(after[backpack].ContainerOwner, Is.EqualTo(SPlayer));
                Assert.That(after[storedItem].ContainerOwner, Is.EqualTo(backpack));
                Assert.That(SEntMan.GetComponent<TransformComponent>(SPlayer).MapUid, Is.EqualTo(MapData.MapUid));
            });
        });
    }

    private Dictionary<EntityUid, ContainedSnapshot> SnapshotContainedDescendants(EntityUid root)
    {
        var containers = SEntMan.System<SharedContainerSystem>();
        var result = new Dictionary<EntityUid, ContainedSnapshot>();
        var pending = new Stack<EntityUid>();
        pending.Push(root);

        while (pending.TryPop(out var parent))
        {
            var children = SEntMan.GetComponent<TransformComponent>(parent).ChildEnumerator;
            while (children.MoveNext(out var child))
            {
                pending.Push(child);
                if (!containers.TryGetContainingContainer(child, out var container))
                    continue;

                var xform = SEntMan.GetComponent<TransformComponent>(child);
                var meta = SEntMan.GetComponent<MetaDataComponent>(child);
                var height = SEntMan.TryGetComponent<ZLevelPresentationComponent>(child, out var presentation)
                    ? presentation.LocalHeight
                    : (float?) null;
                result.Add(child, new ContainedSnapshot(
                    container.Owner,
                    container.ID,
                    xform.ParentUid,
                    xform.MapUid,
                    meta.EntityPrototype?.ID ?? string.Empty,
                    height));
            }
        }

        return result;
    }

    private sealed record ContainedSnapshot(
        EntityUid ContainerOwner,
        string ContainerId,
        EntityUid Parent,
        EntityUid? Map,
        string Prototype,
        float? LocalHeight);
}
