#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.ZLevels;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests.ZLevels;

[TestFixture]
[TestOf(typeof(ZLevelGhostMoverSystem))]
public sealed class ZLevelGhostTraversalTest : InteractionTest
{
    protected override string PlayerPrototype => "MobObserver";

    [Test]
    public async Task IdleObserverNeverFallsOrSnapsToSupport()
    {
        var (maps, _) = await CreateCenteredStack(5);
        Vector2 position = default;

        await Server.WaitAssertion(() =>
        {
            position = Transform.GetWorldPosition(SPlayer);
            AssertObserverState(maps, expectedDepth: 2, position);
        });

        await Pair.RunTicksSync(180);

        await Server.WaitAssertion(() => AssertObserverState(maps, expectedDepth: 2, position));
    }

    [Test]
    public async Task ObserverActionsTraverseOneAdjacentMapAndUpdateEyeRepeatedly()
    {
        var (maps, netMaps) = await CreateCenteredStack(5);
        Entity<ActionComponent> up = default;
        Entity<ActionComponent> down = default;
        Vector2 position = default;

        await Server.WaitAssertion(() =>
        {
            position = Transform.GetWorldPosition(SPlayer);
            up = FindAction<ZLevelGhostMoveUpActionEvent>(SEntMan, Server.System<SharedActionsSystem>(), SPlayer);
            down = FindAction<ZLevelGhostMoveDownActionEvent>(SEntMan, Server.System<SharedActionsSystem>(), SPlayer);
            AssertObserverState(maps, expectedDepth: 2, position);
        });
        await Client.WaitAssertion(() =>
        {
            FindAction<ZLevelGhostMoveUpActionEvent>(CEntMan, Client.System<SharedActionsSystem>(), CPlayer);
            FindAction<ZLevelGhostMoveDownActionEvent>(CEntMan, Client.System<SharedActionsSystem>(), CPlayer);
        });

        await PerformAndSync(up, maps, netMaps, expectedDepth: 3, position, transitionSourceDepth: 2);
        await PerformAndSync(up, maps, netMaps, expectedDepth: 4, position);
        await PerformAndSync(down, maps, netMaps, expectedDepth: 3, position, transitionSourceDepth: 4);
        await PerformAndSync(down, maps, netMaps, expectedDepth: 2, position);
        await PerformAndSync(down, maps, netMaps, expectedDepth: 1, position);
        await PerformAndSync(down, maps, netMaps, expectedDepth: 0, position);
        await PerformAndSync(up, maps, netMaps, expectedDepth: 1, position);
        await PerformAndSync(up, maps, netMaps, expectedDepth: 2, position);

        await Pair.RunTicksSync(120);
        await Server.WaitAssertion(() => AssertObserverState(maps, expectedDepth: 2, position));
        await AssertClientEye(netMaps, expectedDepth: 2, position);
    }

    private async Task PerformAndSync(
        Entity<ActionComponent> action,
        EntityUid[] maps,
        NetEntity[] netMaps,
        int expectedDepth,
        Vector2 position,
        int? transitionSourceDepth = null)
    {
        await Server.WaitPost(() => Server.System<SharedActionsSystem>().PerformAction(SPlayer, action));
        await RunUntilSynced();

        await Server.WaitAssertion(() =>
        {
            AssertObserverState(maps, expectedDepth, position);

            var below = new List<MapId>();
            var above = new List<MapId>();
            var zLevels = SEntMan.System<ZLevelSystem>();
            var mapId = SEntMan.GetComponent<MapComponent>(maps[expectedDepth]).MapId;
            zLevels.CollectRenderableMaps(
                maps[expectedDepth],
                mapId,
                below: 3,
                above: 0,
                default,
                default,
                below,
                above);
            var expectedLower = maps
                .Take(expectedDepth)
                .Reverse()
                .Take(3)
                .Select(map => SEntMan.GetComponent<MapComponent>(map).MapId);
            Assert.Multiple(() =>
            {
                Assert.That(below, Is.EqualTo(expectedLower),
                    "the previous maps must become the observer's lower-only viewport stack");
                Assert.That(above, Is.Empty,
                    "observer interpolation must not expose the otherwise hidden map above");
            });
        });

        await AssertClientEye(netMaps, expectedDepth, position, transitionSourceDepth);
    }

    private async Task AssertClientEye(
        NetEntity[] netMaps,
        int expectedDepth,
        Vector2 position,
        int? transitionSourceDepth = null)
    {
        await Client.WaitAssertion(() =>
        {
            var targetMap = CEntMan.GetEntity(netMaps[expectedDepth]);
            var targetMapId = CEntMan.GetComponent<MapComponent>(targetMap).MapId;
            var xform = CEntMan.GetComponent<TransformComponent>(CPlayer);
            var presentation = CEntMan.GetComponent<ZLevelPresentationComponent>(CPlayer);
            var zPhysics = CEntMan.GetComponent<ZLevelPhysicsComponent>(CPlayer);
            var transforms = CEntMan.System<TransformSystem>();
            var eyes = CEntMan.System<EyeSystem>();

            Assert.Multiple(() =>
            {
                Assert.That(xform.MapUid, Is.EqualTo(targetMap));
                AssertVector(transforms.GetWorldPosition(CPlayer), position);
                Assert.That(presentation.LocalHeight, Is.Zero.Within(0.001f));
                Assert.That(zPhysics.Velocity, Is.Zero.Within(0.001f));
                Assert.That(zPhysics.VelocityGravity, Is.False);
                Assert.That(zPhysics.Fallable, Is.False);
                Assert.That(zPhysics.AutoStep, Is.False);
            });

            if (transitionSourceDepth != null)
                CGameTiming.TickRemainder = CGameTiming.TickPeriod / 2;

            transforms.FrameUpdate(0f);
            eyes.FrameUpdate(0f);
            var eye = CEntMan.GetComponent<EyeComponent>(CPlayer).Eye;
            var pose = transforms.GetRenderWorldPose(CPlayer, xform);

            Assert.Multiple(() =>
            {
                Assert.That(eye.Position.MapId, Is.EqualTo(targetMapId));
                AssertVector(eye.Position.Position, pose.Position);
                AssertVector(pose.CanonicalPosition, position);
            });

            if (transitionSourceDepth is not { } sourceDepth)
                return;

            var samples = new RenderLayerSample[2];
            var count = transforms.GetRenderLayerSamples(CPlayer, samples, xform);
            var lowerDepth = Math.Min(sourceDepth, expectedDepth);
            var upperDepth = Math.Max(sourceDepth, expectedDepth);
            Assert.Multiple(() =>
            {
                Assert.That(pose.AbsoluteZ, Is.GreaterThan(lowerDepth).And.LessThan(upperDepth),
                    "the received map change must remain between its source and target during presentation");
                Assert.That(eye.PresentedAbsoluteZ, Is.EqualTo(pose.AbsoluteZ).Within(0.001f));
                Assert.That(count, Is.EqualTo(2));
                Assert.That(samples[0].Map, Is.EqualTo(CEntMan.GetEntity(netMaps[lowerDepth])));
                Assert.That(samples[1].Map, Is.EqualTo(CEntMan.GetEntity(netMaps[upperDepth])));
                Assert.That(samples[0].Opacity, Is.GreaterThan(0f).And.LessThan(1f));
                Assert.That(samples[1].Opacity, Is.GreaterThan(0f).And.LessThan(1f));
                Assert.That(samples[0].Opacity + samples[1].Opacity, Is.EqualTo(1f).Within(0.001f));
            });
        });
    }

    private void AssertObserverState(EntityUid[] maps, int expectedDepth, Vector2 expectedPosition)
    {
        var xform = SEntMan.GetComponent<TransformComponent>(SPlayer);
        var presentation = SEntMan.GetComponent<ZLevelPresentationComponent>(SPlayer);
        var zPhysics = SEntMan.GetComponent<ZLevelPhysicsComponent>(SPlayer);
        var physicsSystem = SEntMan.System<ZLevelPhysicsSystem>();

        Assert.Multiple(() =>
        {
            Assert.That(xform.MapUid, Is.EqualTo(maps[expectedDepth]));
            AssertVector(Transform.GetWorldPosition(SPlayer), expectedPosition);
            Assert.That(presentation.LocalHeight, Is.Zero.Within(0.001f));
            Assert.That(zPhysics.Velocity, Is.Zero.Within(0.001f));
            Assert.That(zPhysics.VelocityGravity, Is.False);
            Assert.That(zPhysics.Fallable, Is.False);
            Assert.That(zPhysics.AutoStep, Is.False);
            Assert.That(physicsSystem.ActiveBodies, Does.Not.Contain(SPlayer));
        });
    }

    private async Task<(EntityUid[] Maps, NetEntity[] NetMaps)> CreateCenteredStack(int levels)
    {
        var maps = new EntityUid[levels];
        var netMaps = new NetEntity[levels];
        await Server.WaitPost(() =>
        {
            var middle = levels / 2;
            for (var i = 0; i < levels; i++)
                maps[i] = i == middle ? MapData.MapUid : MapSystem.CreateMap(out _);

            Assert.That(SEntMan.System<ZLevelSystem>().TryCreateMapNetwork(maps, out _), Is.True);
            for (var i = 0; i < levels; i++)
                netMaps[i] = SEntMan.GetNetEntity(maps[i]);
        });
        await RunUntilSynced();
        return (maps, netMaps);
    }

    private static Entity<ActionComponent> FindAction<TEvent>(
        IEntityManager entities,
        SharedActionsSystem actions,
        EntityUid holder)
        where TEvent : BaseActionEvent
    {
        var query = entities.GetEntityQuery<InstantActionComponent>();
        return actions.GetActions(holder).Single(action => query.GetComponent(action).Event is TEvent);
    }

    private static void AssertVector(Vector2 actual, Vector2 expected)
    {
        Assert.That(actual.X, Is.EqualTo(expected.X).Within(0.001f));
        Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(0.001f));
    }
}
