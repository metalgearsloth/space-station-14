#nullable enable
using System.Numerics;
using System.Reflection;
using System.Linq;
using Content.Client.Animations;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Helpers;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.CCVar;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Maps;
using Content.Shared.Interaction;
using Content.Shared.Movement.Components;
using Content.Shared.Throwing;
using Content.Shared.ZLevels;
using Robust.Client.Graphics;
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
    private sealed class InteractHandListenerSystem : TestListenerSystem<InteractHandEvent>;

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

- type: entity
  id: ZLevelFlatSupportTestPlatform
  components:
  - type: Transform
    anchored: true
  - type: Physics
    bodyType: Static
  - type: Fixtures
    fixtures:
      zLevelTop:
        shape:
          !type:PhysShapeAabb
          bounds: ""-0.5,-0.5,0.5,0.5""
        hard: false
  - type: ZLevelHighGround
    surfaceFixture: zLevelTop
    height: 0.2
    solidVolume: false

";

    [Test]
    public async Task ProjectedSafeSurfacePrevisIsDisabledByDefault()
    {
        await Client.WaitAssertion(() =>
        {
            var overlays = Client.ResolveDependency<IOverlayManager>();
            Assert.That(overlays.HasOverlay<Content.Client.ZLevels.ZLevelSurfaceOverlay>(), Is.False);
        });
    }

    [Test]
    public async Task PredictedHandPickupLerpsAcrossZLevels()
    {
        var maps = await CreateZStack(2);
        NetEntity itemNet = default;

        await Server.WaitPost(() =>
        {
            Transform.SetCoordinates(SPlayer, new EntityCoordinates(maps[1], new Vector2(0.5f)));
            var playerPhysics = SEntMan.EnsureComponent<ZLevelPhysicsComponent>(SPlayer);
            playerPhysics.VelocityGravity = false;
            SEntMan.System<ZLevelPhysicsSystem>().SetZPosition((SPlayer, playerPhysics), 0f);
            var item = SEntMan.SpawnEntity("Pen", MapData.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            itemNet = SEntMan.GetNetEntity(item);
        });
        await RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var item = CEntMan.GetEntity(itemNet);
            var hands = CEntMan.System<SharedHandsSystem>();
            CEntMan.System<Robust.Client.GameObjects.TransformSystem>().SnapRenderPose(CPlayer);
            Assert.That(hands.TryPickupAnyHand(
                CPlayer,
                item,
                checkActionBlocker: false,
                animate: true), Is.True);

            var query = CEntMan.EntityQueryEnumerator<EntityPickupAnimationComponent, ZLevelPresentationComponent>();
            Assert.That(query.MoveNext(out var clone, out var animation, out var presentation), Is.True,
                "the live hand-pickup path must not discard a legal cross-z animation at its old same-map gate");

            var pickup = CEntMan.System<EntityPickupAnimationSystem>();
            pickup.UpdatePresentation(clone, animation!, presentation!, 0.5f);
            var pose = CEntMan.System<Robust.Client.GameObjects.TransformSystem>().GetRenderWorldPose(clone);
            Assert.Multiple(() =>
            {
                Assert.That(animation.StartAbsoluteZ, Is.EqualTo(0f).Within(0.001f));
                Assert.That(pose.AbsoluteZ, Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(presentation.LocalHeight, Is.EqualTo(0.5f).Within(0.001f));
            });
        });
    }

    [Test]
    public async Task PredictedMovementKeepsConfirmedFlatSupportUntilServerReconciles()
    {
        await CreateZStack(1);
        NetEntity platformNet = default;

        await Server.WaitPost(() =>
        {
            var platform = SpawnFlatPlatform(
                SEntMan,
                MapData.Grid,
                new Vector2(0.5f),
                0.2f,
                solidVolume: false);
            platformNet = SEntMan.GetNetEntity(platform);

            Transform.SetCoordinates(SPlayer, new EntityCoordinates(MapData.Grid, new Vector2(0.5f)));
            SEntMan.System<SharedPhysicsSystem>().SetBodyType(SPlayer, BodyType.Dynamic);

            var vertical = SEntMan.EnsureComponent<ZLevelPhysicsComponent>(SPlayer);
            var zPhysics = SEntMan.System<ZLevelPhysicsSystem>();
            zPhysics.SetZPosition((SPlayer, vertical), 0.2f);
            vertical.GroundState = ZLevelGroundState.Grounded;
            zPhysics.RefreshSupport((SPlayer, vertical));
        });
        await RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var platform = CEntMan.GetEntity(platformNet);
            var vertical = CEntMan.GetComponent<ZLevelPhysicsComponent>(CPlayer);
            var presentation = CEntMan.GetComponent<ZLevelPresentationComponent>(CPlayer);
            var transforms = CEntMan.System<Robust.Client.GameObjects.TransformSystem>();
            var oldAuthoritativeHeight = vertical.SupportHeight;

            Assert.That(vertical.SupportProvider, Is.EqualTo(platform));
            // InteractionTestMob's bare Physics component defaults to Static independently on each side.
            CEntMan.System<SharedPhysicsSystem>().SetBodyType(CPlayer, BodyType.Dynamic);
            transforms.SetWorldPosition(CPlayer, new Vector2(0.55f, 0.5f));
            CEntMan.System<ZLevelPhysicsSystem>().Update(1f);

            Assert.Multiple(() =>
            {
                Assert.That(presentation.LocalHeight, Is.EqualTo(0.2f).Within(0.001f),
                    "flat same-provider predicted XY movement must not mutate replicated vertical presentation");
                Assert.That(vertical.SupportHeight, Is.EqualTo(oldAuthoritativeHeight).Within(0.001f),
                    "prediction must not overwrite the replicated confirmed support state");
                Assert.That(vertical.GroundState, Is.EqualTo(ZLevelGroundState.Grounded));
            });
        });

        await Server.WaitPost(() =>
        {
            var platform = SEntMan.GetEntity(platformNet);
            var highGround = SEntMan.GetComponent<ZLevelHighGroundComponent>(platform);
            var zPhysics = SEntMan.System<ZLevelPhysicsSystem>();
            zPhysics.SetSupportHeight((platform, highGround), 0.24f);

            var vertical = SEntMan.GetComponent<ZLevelPhysicsComponent>(SPlayer);
            Transform.SetCoordinates(SPlayer, new EntityCoordinates(MapData.Grid, new Vector2(0.55f, 0.5f)));
            vertical.GroundState = ZLevelGroundState.Grounded;
            zPhysics.RefreshSupport((SPlayer, vertical));
        });
        await RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var vertical = CEntMan.GetComponent<ZLevelPhysicsComponent>(CPlayer);
            var presentation = CEntMan.GetComponent<ZLevelPresentationComponent>(CPlayer);
            Assert.Multiple(() =>
            {
                Assert.That(vertical.SupportHeight, Is.EqualTo(0.24f).Within(0.001f));
                Assert.That(presentation.LocalHeight, Is.EqualTo(0.24f).Within(0.001f));
                Assert.That(vertical.GroundState, Is.EqualTo(ZLevelGroundState.Grounded));
            });
        });
    }

    [Test]
    public async Task AuthoritativeFallAndLandingNeverToggleControlledPlayerPrediction()
    {
        var maps = await CreateZStack(2);
        await BuildRunway(2);
        NetEntity platformNet = default;
        EntityUid upperGrid = default;

        await Server.WaitPost(() =>
        {
            var upperMap = SEntMan.GetComponent<MapComponent>(maps[1]);
            upperGrid = MapSystem.CreateGridEntity(upperMap.MapId).Owner;
            Assert.That(SEntMan.System<ZLevelSystem>().TryLinkGrids(MapData.Grid.Owner, upperGrid), Is.True);
            var platform = SpawnFlatPlatform(
                SEntMan,
                upperGrid,
                new Vector2(0.5f),
                0.8f,
                solidVolume: false);
            platformNet = SEntMan.GetNetEntity(platform);
        });

        // Let the anchored support fixture enter the broadphase before placing the controlled body on it.
        await Server.WaitRunTicks(1);
        await Server.WaitPost(() =>
        {
            Transform.SetCoordinates(SPlayer, new EntityCoordinates(upperGrid, new Vector2(0.5f)));
            var physics = SEntMan.GetComponent<PhysicsComponent>(SPlayer);
            SEntMan.System<SharedPhysicsSystem>().SetBodyType(
                SPlayer,
                BodyType.KinematicController,
                body: physics);

            var vertical = SEntMan.EnsureComponent<ZLevelPhysicsComponent>(SPlayer);
            var zPhysics = SEntMan.System<ZLevelPhysicsSystem>();
            zPhysics.SetZPosition((SPlayer, vertical), 0.8f);
            vertical.GroundState = ZLevelGroundState.Grounded;
            zPhysics.RefreshSupport((SPlayer, vertical));

            Assert.Multiple(() =>
            {
                Assert.That(vertical.SupportProvider, Is.EqualTo(SEntMan.GetEntity(platformNet)));
                Assert.That(vertical.GroundState, Is.EqualTo(ZLevelGroundState.Grounded));
            });
        });
        await RunUntilSynced();

        EntityUid confirmedSupport = default;
        EntityUid confirmedMap = default;
        await Client.WaitAssertion(() =>
        {
            var platform = CEntMan.GetEntity(platformNet);
            var physics = CEntMan.GetComponent<PhysicsComponent>(CPlayer);
            var clientPhysics = CEntMan.System<Robust.Client.Physics.PhysicsSystem>();
            clientPhysics.SetBodyType(CPlayer, BodyType.KinematicController, body: physics);
            CEntMan.EnsureComponent<InputMoverComponent>(CPlayer);
            clientPhysics.UpdateIsPredicted(CPlayer, physics);
            clientPhysics.Update(0f);

            var vertical = CEntMan.GetComponent<ZLevelPhysicsComponent>(CPlayer);
            var xform = CEntMan.GetComponent<TransformComponent>(CPlayer);
            confirmedSupport = vertical.SupportProvider!.Value;
            confirmedMap = xform.MapUid!.Value;
            Assert.Multiple(() =>
            {
                Assert.That(physics.Predict, Is.True,
                    "ordinary movement on confirmed high ground must be predicted");
                Assert.That(vertical.SupportProvider, Is.EqualTo(platform));
                Assert.That(vertical.GroundState, Is.EqualTo(ZLevelGroundState.Grounded));
            });

            // Model the unacknowledged XY result visible under latency. The client is deliberately outside the
            // confirmed footprint, but the client-side z system must not infer support loss or begin gravity.
            CEntMan.System<TransformSystem>().SetWorldPosition(CPlayer, new Vector2(1.2f, 0.5f));
            CEntMan.System<ZLevelPhysicsSystem>().Update(1f);
            Assert.Multiple(() =>
            {
                Assert.That(CEntMan.System<TransformSystem>().GetWorldPosition(CPlayer).X,
                    Is.EqualTo(1.2f).Within(0.001f),
                    "walking off the confirmed edge must retain immediate predicted XY response");
                Assert.That(vertical.SupportProvider, Is.EqualTo(confirmedSupport));
                Assert.That(vertical.GroundState, Is.EqualTo(ZLevelGroundState.Grounded),
                    "the client must not locally begin the fall");
                Assert.That(xform.MapUid, Is.EqualTo(confirmedMap));
                Assert.That(physics.Predict, Is.True);
            });
        });

        // The server reaches the same XY point, authoritatively selects the lower floor, and begins the fall.
        await Server.WaitPost(() =>
        {
            Transform.SetCoordinates(SPlayer, new EntityCoordinates(maps[1], new Vector2(1.2f, 0.5f)));
            var vertical = SEntMan.GetComponent<ZLevelPhysicsComponent>(SPlayer);
            SEntMan.System<ZLevelPhysicsSystem>().RefreshSupport((SPlayer, vertical));
            Assert.Multiple(() =>
            {
                Assert.That(vertical.GroundState, Is.EqualTo(ZLevelGroundState.Airborne));
                Assert.That(vertical.SupportProvider, Is.EqualTo(MapData.Grid.Owner));
            });
        });
        await RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var physics = CEntMan.GetComponent<PhysicsComponent>(CPlayer);
            var vertical = CEntMan.GetComponent<ZLevelPhysicsComponent>(CPlayer);
            var xform = CEntMan.GetComponent<TransformComponent>(CPlayer);
            Assert.Multiple(() =>
            {
                Assert.That(vertical.GroundState, Is.EqualTo(ZLevelGroundState.Airborne));
                Assert.That(vertical.SupportProvider, Is.EqualTo(MapData.CGridUid));
                Assert.That(xform.MapUid, Is.EqualTo(confirmedMap),
                    "the client must wait for the authoritative z-map crossing");
                Assert.That(physics.Predict, Is.True,
                    "receiving an authoritative fall must not disable normal XY prediction");
            });

            // Replaying the same state cannot turn the authoritative lower support back into the old platform.
            CEntMan.System<ZLevelPhysicsSystem>().Update(1f);
            Assert.Multiple(() =>
            {
                Assert.That(vertical.GroundState, Is.EqualTo(ZLevelGroundState.Airborne));
                Assert.That(vertical.SupportProvider, Is.EqualTo(MapData.CGridUid));
                Assert.That(physics.Predict, Is.True);
            });
        });

        // Allow the authoritative vertical solver to cross maps and land on the lower runway.
        await RunSeconds(1f);
        await RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var physics = CEntMan.GetComponent<PhysicsComponent>(CPlayer);
            var vertical = CEntMan.GetComponent<ZLevelPhysicsComponent>(CPlayer);
            var xform = CEntMan.GetComponent<TransformComponent>(CPlayer);
            var transforms = CEntMan.System<TransformSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(xform.MapUid, Is.EqualTo(MapData.CMapUid));
                Assert.That(vertical.GroundState, Is.EqualTo(ZLevelGroundState.Grounded));
                Assert.That(vertical.SupportProvider, Is.EqualTo(MapData.CGridUid));
                Assert.That(physics.Predict, Is.True,
                    "authoritative landing must not re-enable a mode that was never disabled");
            });

            var landedX = transforms.GetWorldPosition(CPlayer).X;
            transforms.SetWorldPosition(CPlayer, new Vector2(landedX + 0.1f, 0.5f));
            Assert.Multiple(() =>
            {
                Assert.That(transforms.GetWorldPosition(CPlayer).X, Is.EqualTo(landedX + 0.1f).Within(0.001f),
                    "movement immediately after landing must stay responsive");
                Assert.That(vertical.GroundState, Is.EqualTo(ZLevelGroundState.Grounded));
                Assert.That(physics.Predict, Is.True);
            });
        });
    }

    [TestCase(0f)]
    [TestCase(0.7f)]
    public async Task LowerLevelInteractionCandidatesAgreeAcrossClientAndServer(float projectionOffset)
    {
        var maps = await CreateZStack(2);
        NetEntity targetNet = default;
        EntityUid target = default;

        await Server.WaitPost(() =>
        {
            var zLevels = SEntMan.System<ZLevelSystem>();
            Assert.That(zLevels.TryGetMapData(maps[1], out var upper, out _), Is.True);
            zLevels.SetProjectionOffset(upper!.Network, new Vector2(0f, projectionOffset));

            Transform.SetCoordinates(SPlayer, new EntityCoordinates(maps[1], Vector2.Zero));
            target = SEntMan.SpawnEntity("Pen", MapData.GridCoords.Offset(new Vector2(0.5f, 0f)));
            SEntMan.EnsureComponent<TestListenerComponent>(target);
            targetNet = SEntMan.GetNetEntity(target);
            Assert.That(InteractSys.InRangeUnobstructed(SPlayer, target), Is.True);
            InteractSys.UserInteraction(SPlayer, SEntMan.GetComponent<TransformComponent>(target).Coordinates, target);

            var farTarget = SEntMan.SpawnEntity("Pen", MapData.GridCoords.Offset(new Vector2(2f, 0f)));
            Assert.That(InteractSys.InRangeUnobstructed(SPlayer, farTarget), Is.False);
            Assert.That(InteractSys.ShouldShowProjectedInteraction(SPlayer, farTarget), Is.False);
        });
        AssertEvent<InteractHandEvent>(target);

        await RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var target = CEntMan.GetEntity(targetNet);
            var interactions = CEntMan.System<Content.Client.Interactable.InteractionSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(interactions.InRangeUnobstructed(CPlayer, target), Is.True);
                Assert.That(interactions.ShouldShowProjectedInteraction(CPlayer, target), Is.True);
            });
        });
    }

    [Test]
    public async Task LinkedMovingGridInteractionAgreesAcrossClientAndServer()
    {
        var maps = await CreateZStack(2);
        NetEntity targetNet = default;

        await Server.WaitPost(() =>
        {
            var zLevels = SEntMan.System<ZLevelSystem>();
            var upperMap = SEntMan.GetComponent<MapComponent>(maps[1]);
            var upperGrid = MapSystem.CreateGridEntity(upperMap.MapId);
            Assert.That(zLevels.TryLinkGrids(MapData.Grid, upperGrid), Is.True);
            Assert.That(zLevels.TryMoveLinkedGrids(
                MapData.Grid,
                new Vector2(4f, -2f),
                Angle.FromDegrees(90f)), Is.True);

            Transform.SetCoordinates(SPlayer, new EntityCoordinates(upperGrid, new Vector2(0.5f, 0.5f)));
            var target = SEntMan.SpawnEntity("Pen", new EntityCoordinates(MapData.Grid, new Vector2(0.5f, 0.5f)));
            targetNet = SEntMan.GetNetEntity(target);
            Assert.Multiple(() =>
            {
                Assert.That(InteractSys.InRangeUnobstructed(SPlayer, target), Is.True);
                Assert.That(InteractSys.ShouldShowProjectedInteraction(SPlayer, target), Is.True);
            });
        });

        await RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var target = CEntMan.GetEntity(targetNet);
            var interactions = CEntMan.System<Content.Client.Interactable.InteractionSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(interactions.InRangeUnobstructed(CPlayer, target), Is.True);
                Assert.That(interactions.ShouldShowProjectedInteraction(CPlayer, target), Is.True);
            });
        });
    }

    [Test]
    public async Task SolidUpperFloorRejectsLowerLevelInteractionCandidate()
    {
        await Server.WaitPost(() =>
        {
            var lower = MapSystem.CreateMap(out _);
            Assert.That(SEntMan.System<ZLevelSystem>().TryCreateMapNetwork([lower, MapData.MapUid], out _), Is.True);
            Transform.SetCoordinates(SPlayer, MapData.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            var target = SEntMan.SpawnEntity(null, new EntityCoordinates(lower, new Vector2(0.5f, 0.5f)));
            Assert.Multiple(() =>
            {
                Assert.That(InteractSys.InRangeUnobstructed(SPlayer, target), Is.False);
                Assert.That(InteractSys.ShouldShowProjectedInteraction(SPlayer, target), Is.False);
            });
        });
    }

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

    private static EntityUid SpawnFlatPlatform(
        IEntityManager entities,
        EntityUid parent,
        Vector2 localPosition,
        float height,
        bool solidVolume)
    {
        var uid = entities.SpawnEntity("ZLevelFlatSupportTestPlatform", new EntityCoordinates(parent, localPosition));
        var highGround = entities.GetComponent<ZLevelHighGroundComponent>(uid);
        SetHighGroundField(highGround, nameof(ZLevelHighGroundComponent.Height), height);
        SetHighGroundField(highGround, nameof(ZLevelHighGroundComponent.SolidVolume), solidVolume);
        return uid;
    }

    private static void SetHighGroundField<T>(ZLevelHighGroundComponent component, string field, T value)
    {
        typeof(ZLevelHighGroundComponent)
            .GetField(field)!
            .SetValue(component, value);
    }
}
