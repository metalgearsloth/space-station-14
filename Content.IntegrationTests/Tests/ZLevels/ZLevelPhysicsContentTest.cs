#nullable enable
using System.Numerics;
using System.Reflection;
using System.Linq;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Helpers;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.CCVar;
using Content.Shared.Damage.Systems;
using Content.Shared.Maps;
using Content.Shared.Throwing;
using Content.Shared.ZLevels;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests.ZLevels;

[TestFixture]
[TestOf(typeof(ZLevelPhysicsContentSystem))]
public sealed class ZLevelPhysicsContentTest : InteractionTest
{
    private sealed class LandListenerSystem : TestListenerSystem<LandEvent>;

    [SidedDependency(Side.Server)] private readonly ThrowingSystem _throwing = default!;
    [SidedDependency(Side.Server)] private readonly ThrownItemSystem _thrown = default!;
    [SidedDependency(Side.Server)] private readonly DamageableSystem _damageable = default!;

    [TestPrototypes]
    private const string ZThrowTestPrototypes = @"
- type: entity
  id: ZLevelThrowDamageItem
  parent: Pen
  components:
  - type: DamageOtherOnHit
    damage:
      types:
        Piercing: 3

";

    [Test]
    public async Task ProjectedLowerLayerDropUsesCanonicalPositionOnViewedMap()
    {
        var maps = await CreateZStack(3);

        await Server.WaitPost(() =>
        {
            var zLevels = SEntMan.System<ZLevelSystem>();
            Assert.That(zLevels.TryGetMapData(maps[2], out var viewedZ, out _), Is.True);
            zLevels.SetProjectionOffset(viewedZ!.Network, new Vector2(0f, 0.7f));

            var canonicalTarget = new Vector2(0.5f, 0.75f);
            var displayedTarget = ZLevelProjection.Project(canonicalTarget, 0f, 2, new Vector2(0f, 0.7f));
            var clickedLowerEntity = SEntMan.SpawnEntity(null, new EntityCoordinates(maps[0], canonicalTarget));
            var item = SEntMan.SpawnEntity("Pen", new EntityCoordinates(maps[2], Vector2.Zero));
            Transform.SetCoordinates(SPlayer, new EntityCoordinates(maps[2], Vector2.Zero));

            Assert.That(HandSys.TryPickupAnyHand(
                SPlayer,
                item,
                checkActionBlocker: false,
                animate: false), Is.True);

            var displayedCoordinates = new EntityCoordinates(maps[2], displayedTarget);
            var resolvedCoordinates = HandSys.ResolveProjectedDropCoordinates(
                displayedCoordinates,
                clickedLowerEntity);
            var resolvedMapCoordinates = Transform.ToMapCoordinates(resolvedCoordinates);

            Assert.Multiple(() =>
            {
                Assert.That(resolvedMapCoordinates.MapId,
                    Is.EqualTo(SEntMan.GetComponent<MapComponent>(maps[2]).MapId));
                Assert.That(resolvedMapCoordinates.Position.X, Is.EqualTo(canonicalTarget.X).Within(0.001f));
                Assert.That(resolvedMapCoordinates.Position.Y, Is.EqualTo(canonicalTarget.Y).Within(0.001f));
            });

            Assert.That(HandSys.TryDrop(
                SPlayer,
                resolvedCoordinates,
                checkActionBlocker: false,
                doDropInteraction: false), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(Transform.GetMap(item), Is.EqualTo(maps[2]));
                Assert.That(Transform.GetWorldPosition(item).X, Is.EqualTo(canonicalTarget.X).Within(0.001f));
                Assert.That(Transform.GetWorldPosition(item).Y, Is.EqualTo(canonicalTarget.Y).Within(0.001f));
            });
        });
    }

    [Test]
    public async Task CeilingContactDoesNotRaiseContentLandEvent()
    {
        await Server.WaitPost(() =>
        {
            SEntMan.EnsureComponent<TestListenerComponent>(SPlayer);
            SEntMan.EnsureComponent<ZLevelPhysicsComponent>(SPlayer);
        });
        ClearEvents<LandEvent>(SPlayer);

        await Server.WaitPost(() =>
        {
            var ceiling = new ZLevelLandingEvent(5f, ZLevelImpactSurface.Ceiling);
            SEntMan.EventBus.RaiseLocalEvent(SPlayer, ref ceiling, true);
        });
        AssertEvent<LandEvent>(SPlayer, count: 0);

        await Server.WaitPost(() =>
        {
            var floor = new ZLevelLandingEvent(5f, ZLevelImpactSurface.Floor);
            SEntMan.EventBus.RaiseLocalEvent(SPlayer, ref floor, true);
        });
        AssertEvent<LandEvent>(SPlayer, count: 1);
    }

    [Test]
    public async Task HorizontalSleepDoesNotEndAnAirborneVerticalThrow()
    {
        await CreateZStack(3);
        EntityUid item = default;

        await Server.WaitPost(() =>
        {
            item = SEntMan.SpawnEntity("Pen", MapData.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            Assert.That(_throwing.TryThrow(item, Vector2.UnitX, baseThrowSpeed: 4f, playSound: false), Is.True);

            var body = SEntMan.GetComponent<PhysicsComponent>(item);
            var vertical = SEntMan.GetComponent<ZLevelPhysicsComponent>(item);
            SEntMan.System<ZLevelPhysicsSystem>().SetZVelocity((item, vertical), 8f);
            var sleep = new PhysicsSleepEvent(item, body);
            SEntMan.EventBus.RaiseLocalEvent(item, ref sleep, true);

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<ThrownItemComponent>(item).VerticalPhysics, Is.True);
                Assert.That(SEntMan.HasComponent<ThrownItemComponent>(item), Is.True);
                Assert.That(SentIsActive(item), Is.True);
            });
        });
    }

    [Test]
    public async Task ActualItemFallsFiveMapsAndLandsOnlyOnce()
    {
        var maps = await CreateZStack(6);
        EntityUid item = default;

        await Server.WaitPost(() =>
        {
            var bottomMap = SEntMan.GetComponent<MapComponent>(maps[0]);
            var position = Transform.ToMapCoordinates(MapData.GridCoords.Offset(new Vector2(0.5f, 0.5f))).Position;
            item = SEntMan.SpawnEntity("Pen", new MapCoordinates(position, SEntMan.GetComponent<MapComponent>(maps[5]).MapId));
            SEntMan.EnsureComponent<TestListenerComponent>(item);

            Assert.Multiple(() =>
            {
                Assert.That(bottomMap.MapId, Is.EqualTo(MapData.MapId));
                Assert.That(SentIsActive(item), Is.True);
                Assert.That(GetDepth(item), Is.EqualTo(5));
            });
        });
        ClearEvents<LandEvent>(item);

        await Pair.RunTicksSync(120);
        await Server.WaitAssertion(() =>
        {
            var presentation = SEntMan.GetComponent<ZLevelPresentationComponent>(item);
            var vertical = SEntMan.GetComponent<ZLevelPhysicsComponent>(item);
            Assert.Multiple(() =>
            {
                Assert.That(GetDepth(item), Is.Zero);
                Assert.That(presentation.LocalHeight, Is.Zero.Within(0.001f));
                Assert.That(vertical.Velocity, Is.Zero.Within(0.001f));
            });
        });
        AssertEvent<LandEvent>(item, count: 1);

        await Pair.RunTicksSync(60);
        AssertEvent<LandEvent>(item, count: 1);
    }

    [Test]
    public async Task ThrowRetainsZPhysicsDamageAndHorizontalMotionAcrossMultiLevelArc()
    {
        await OverrideCVar(Side.Server, CCVars.TileFrictionModifier, 0f);
        await OverrideCVar(Side.Server, CCVars.AirFriction, 0f);
        await OverrideCVar(Side.Server, CCVars.OffgridFriction, 0f);
        await OverrideCVar(Side.Server, CCVars.MinFriction, 0f);
        await CreateZStack(10);
        await BuildRunway(40);
        await AddGravity();

        EntityUid item = default;
        NetEntity netItem = default;
        EntityUid target = default;
        float initialX = 0f;
        float horizontalVelocity = 0f;
        TimeSpan? oldTimer = null;

        await Server.WaitPost(() =>
        {
            item = SEntMan.SpawnEntity(
                "ZLevelThrowDamageItem",
                MapData.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            netItem = SEntMan.GetNetEntity(item);
            target = SEntMan.SpawnEntity(
                "MobMonkey",
                MapData.GridCoords.Offset(new Vector2(20.5f, 0.5f)));
            SEntMan.EnsureComponent<TestListenerComponent>(item);

            Assert.That(_throwing.TryThrow(
                item,
                new Vector2(4f, 0f),
                baseThrowSpeed: 4f,
                user: SPlayer,
                playSound: false), Is.True);

            var thrown = SEntMan.GetComponent<ThrownItemComponent>(item);
            var vertical = SEntMan.GetComponent<ZLevelPhysicsComponent>(item);
            var body = SEntMan.GetComponent<PhysicsComponent>(item);
            var zPhysics = SEntMan.System<ZLevelPhysicsSystem>();
            zPhysics.SetZVelocity((item, vertical), 12f);

            initialX = Transform.GetWorldPosition(item).X;
            horizontalVelocity = body.LinearVelocity.X;
            oldTimer = thrown.LandTime;

            Assert.Multiple(() =>
            {
                Assert.That(thrown.VerticalPhysics, Is.True);
                Assert.That(vertical.Velocity, Is.EqualTo(12f));
                Assert.That(horizontalVelocity, Is.GreaterThan(0f));
                Assert.That(zPhysics.ActiveBodies, Does.Contain(item));
                Assert.That(body.BodyStatus, Is.EqualTo(BodyStatus.InAir));
            });
        });
        ClearEvents<LandEvent>(item);

        // The rising half crosses several maps without ending horizontal movement or throw hit behavior.
        await Pair.RunTicksSync(20);
        await Server.WaitPost(() =>
        {
            var vertical = SEntMan.GetComponent<ZLevelPhysicsComponent>(item);
            var thrown = SEntMan.GetComponent<ThrownItemComponent>(item);
            var body = SEntMan.GetComponent<PhysicsComponent>(item);
            var before = _damageable.GetTotalDamage(target);
            var depth = GetDepth(item);

            _thrown.ThrowCollideInteraction(thrown, item, target);
            var after = _damageable.GetTotalDamage(target);

            Assert.Multiple(() =>
            {
                Assert.That(depth, Is.GreaterThanOrEqualTo(4));
                Assert.That(vertical.Velocity, Is.GreaterThan(0f));
                Assert.That(SEntMan.HasComponent<ThrownItemComponent>(item), Is.True);
                Assert.That(body.LinearVelocity.X, Is.EqualTo(horizontalVelocity).Within(0.01f));
                Assert.That(Transform.GetWorldPosition(item).X, Is.GreaterThan(initialX));
                Assert.That(after, Is.GreaterThan(before), "throw damage was lost after a z-level reparent");
            });
        });

        // This is past the legacy timer. The descending half and client replay path must still keep the throw alive.
        await Pair.RunTicksSync(25);
        await RunUntilSynced();
        await Server.WaitAssertion(() =>
        {
            var vertical = SEntMan.GetComponent<ZLevelPhysicsComponent>(item);
            var body = SEntMan.GetComponent<PhysicsComponent>(item);
            Assert.Multiple(() =>
            {
                Assert.That(STiming.CurTime, Is.GreaterThan(oldTimer!.Value));
                Assert.That(vertical.Velocity, Is.LessThan(0f));
                Assert.That(GetDepth(item), Is.GreaterThan(0));
                Assert.That(SEntMan.GetComponent<ThrownItemComponent>(item).VerticalPhysics, Is.True);
                Assert.That(body.LinearVelocity.X, Is.EqualTo(horizontalVelocity).Within(0.01f));
            });
        });
        await Client.WaitAssertion(() =>
        {
            var clientItem = CEntMan.GetEntity(netItem);
            var thrownSystem = CEntMan.System<ThrownItemSystem>();
            Assert.That(CEntMan.GetComponent<ThrownItemComponent>(clientItem).VerticalPhysics, Is.True);
            thrownSystem.Update(1f);
            Assert.That(CEntMan.HasComponent<ThrownItemComponent>(clientItem), Is.True,
                "prediction replay used the legacy timer to land a server-authoritative vertical throw");
        });

        // The real floor impact lands once, removes only throw collision state, and preserves intentional sliding.
        await Pair.RunTicksSync(40);
        await Server.WaitAssertion(() =>
        {
            var presentation = SEntMan.GetComponent<ZLevelPresentationComponent>(item);
            var body = SEntMan.GetComponent<PhysicsComponent>(item);
            Assert.Multiple(() =>
            {
                Assert.That(GetDepth(item), Is.Zero);
                Assert.That(presentation.LocalHeight, Is.EqualTo(0f).Within(0.001f));
                Assert.That(SEntMan.HasComponent<ThrownItemComponent>(item), Is.False);
                Assert.That(body.LinearVelocity.X, Is.GreaterThan(0f), "landing incorrectly removed post-land sliding");
                Assert.That(body.BodyStatus, Is.EqualTo(BodyStatus.OnGround));
            });
        });

        AssertEvent<LandEvent>(item, count: 1);
    }

    [Test]
    public async Task ClientDoesNotAdvanceReplicatedVerticalState()
    {
        await CreateZStack(3);

        EntityUid item = default;
        NetEntity netItem = default;
        await Server.WaitPost(() =>
        {
            item = SEntMan.SpawnEntity("Pen", MapData.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            netItem = SEntMan.GetNetEntity(item);
            var vertical = SEntMan.GetComponent<ZLevelPhysicsComponent>(item);
            SetVelocityGravity(vertical, false);
            var zPhysics = SEntMan.System<ZLevelPhysicsSystem>();
            zPhysics.SetZPosition((item, vertical), 0.4f);
            zPhysics.SetZVelocity((item, vertical), 5f);
        });
        await RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var clientItem = CEntMan.GetEntity(netItem);
            var presentation = CEntMan.GetComponent<ZLevelPresentationComponent>(clientItem);
            var beforeHeight = presentation.LocalHeight;
            var beforeMap = CEntMan.GetComponent<TransformComponent>(clientItem).MapUid;
            var transforms = CEntMan.System<TransformSystem>();
            var predictedPosition = transforms.GetWorldPosition(clientItem) + new Vector2(0.1f, 0f);
            transforms.SetWorldPosition(clientItem, predictedPosition);
            var beforeSupport = CEntMan.GetComponent<ZLevelPhysicsComponent>(clientItem);
            var supportProvider = beforeSupport.SupportProvider;
            var supportHeight = beforeSupport.SupportHeight;
            var groundState = beforeSupport.GroundState;

            CEntMan.System<ZLevelPhysicsSystem>().Update(1f);

            Assert.Multiple(() =>
            {
                Assert.That(presentation.LocalHeight, Is.EqualTo(beforeHeight));
                Assert.That(CEntMan.GetComponent<TransformComponent>(clientItem).MapUid, Is.EqualTo(beforeMap));
                Assert.That(transforms.GetWorldPosition(clientItem), Is.EqualTo(predictedPosition),
                    "server-authoritative z simulation disabled or rewound ordinary predicted XY movement");
                Assert.That(beforeSupport.SupportProvider, Is.EqualTo(supportProvider));
                Assert.That(beforeSupport.SupportHeight, Is.EqualTo(supportHeight));
                Assert.That(beforeSupport.GroundState, Is.EqualTo(groundState));
            });
        });
    }

    private bool SentIsActive(EntityUid item)
        => SEntMan.System<ZLevelPhysicsSystem>().ActiveBodies.Contains(item);

    private async Task<EntityUid[]> CreateZStack(int levels)
    {
        var maps = new EntityUid[levels];
        await Server.WaitPost(() =>
        {
            maps[0] = MapData.MapUid;
            for (var i = 1; i < maps.Length; i++)
                maps[i] = MapSystem.CreateMap(out _);

            Assert.That(SEntMan.System<ZLevelSystem>().TryCreateMapNetwork(maps, out _), Is.True);
        });
        return maps;
    }

    private async Task BuildRunway(int length)
    {
        await Server.WaitPost(() =>
        {
            var tile = new Tile(TileMan[Plating].TileId);
            for (var x = 0; x <= length; x++)
                MapSystem.SetTile(MapData.Grid.Owner, MapData.Grid.Comp, new Vector2i(x, 0), tile);
        });
    }

    private int GetDepth(EntityUid uid)
    {
        var map = Transform.GetMap(uid);
        Assert.That(map, Is.Not.Null);
        Assert.That(SEntMan.System<ZLevelSystem>().TryGetMapDepth(map!.Value, out var depth), Is.True);
        return depth!.Value;
    }

    private static void SetVelocityGravity(ZLevelPhysicsComponent component, bool value)
    {
        typeof(ZLevelPhysicsComponent)
            .GetField(nameof(ZLevelPhysicsComponent.VelocityGravity))!
            .SetValue(component, value);
    }
}
