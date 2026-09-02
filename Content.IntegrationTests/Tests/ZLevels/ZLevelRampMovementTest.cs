#nullable enable
using System.Numerics;
using Content.IntegrationTests.Tests.Movement;
using Content.Shared.Movement.Components;
using Content.Shared.ZLevels;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests.ZLevels;

[TestFixture]
public sealed class ZLevelRampMovementTest : MovementTest
{
    protected override int Tiles => 5;
    protected override bool AddWalls => false;

    [TestCase(0f)]
    [TestCase(90f)]
    [TestCase(180f)]
    [TestCase(270f)]
    public async Task RampTriggerTraversesLinkedGridBothDirections(float rotationDegrees)
    {
        EntityUid upperMap = default;
        EntityUid upperGrid = default;
        EntityUid lowerRamp = default;
        EntityUid upperRamp = default;
        var rampPosition = new Vector2(0.5f, 0.5f);
        var rotation = Angle.FromDegrees(rotationDegrees);
        var heading = rotation.RotateVec(Vector2.UnitY);

        await Server.WaitPost(() =>
        {
            (upperMap, upperGrid) = CreateLinkedStack();
            PlacePlayer(MapData.Grid.Owner, rampPosition - heading * 0.2f);
            lowerRamp = SEntMan.SpawnAttachedTo(
                "ZLevelRampUp",
                new EntityCoordinates(MapData.Grid.Owner, rampPosition),
                rotation: rotation);
        });
        await RunTicks(4);

        await Server.WaitAssertion(() =>
        {
            var lowerPortal = SEntMan.GetComponent<ZLevelPortalComponent>(lowerRamp);
            Assert.That(lowerPortal.PairedEndpoint, Is.Not.Null);
            upperRamp = lowerPortal.PairedEndpoint!.Value;

            AssertRampPrototype(lowerRamp, expectedOffset: 1);
            AssertRampPrototype(upperRamp, expectedOffset: -1);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<TransformComponent>(upperRamp).MapUid, Is.EqualTo(upperMap));
                Assert.That(SEntMan.GetComponent<TransformComponent>(upperRamp).GridUid, Is.EqualTo(upperGrid));
                Assert.That(
                    Transform.GetWorldRotation(upperRamp).Theta,
                    Is.EqualTo(rotation.Theta).Within(0.001f));
            });
        });

        await Server.WaitPost(() => CrossTrigger(SPlayer, MapData.Grid.Owner, rampPosition, heading, forward: true));

        await Server.WaitAssertion(() =>
        {
            var xform = SEntMan.GetComponent<TransformComponent>(SPlayer);
            Assert.Multiple(() =>
            {
                Assert.That(xform.MapUid, Is.EqualTo(upperMap));
                Assert.That(xform.GridUid, Is.EqualTo(upperGrid));
            });
        });

        await Server.WaitPost(() => CrossTrigger(SPlayer, upperGrid, rampPosition, heading, forward: false));

        await Server.WaitAssertion(() =>
        {
            var xform = SEntMan.GetComponent<TransformComponent>(SPlayer);
            Assert.Multiple(() =>
            {
                Assert.That(xform.MapUid, Is.EqualTo(MapData.MapUid));
                Assert.That(xform.GridUid, Is.EqualTo(MapData.Grid.Owner));
            });
        });
    }

    [Test]
    public async Task RampPortalFallsBackToMapWhenNoLinkedGridExists()
    {
        EntityUid upperMap = default;
        EntityUid lowerRamp = default;
        var rampPosition = new Vector2(0.5f, 0.5f);

        await Server.WaitPost(() =>
        {
            upperMap = CreateUnlinkedUpperMap();
            FillGrid(MapData.Grid.Owner);
            PlacePlayer(MapData.Grid.Owner, rampPosition - new Vector2(0f, 0.2f));
            lowerRamp = SEntMan.SpawnAttachedTo(
                "ZLevelRampUp",
                new EntityCoordinates(MapData.Grid.Owner, rampPosition));
        });
        await RunTicks(4);

        await Server.WaitAssertion(() =>
        {
            var lowerPortal = SEntMan.GetComponent<ZLevelPortalComponent>(lowerRamp);
            Assert.That(lowerPortal.PairedEndpoint, Is.Not.Null);

            var paired = lowerPortal.PairedEndpoint!.Value;
            var pairedXform = SEntMan.GetComponent<TransformComponent>(paired);
            Assert.Multiple(() =>
            {
                Assert.That(pairedXform.MapUid, Is.EqualTo(upperMap));
                Assert.That(pairedXform.GridUid, Is.Null);
            });
        });

        await Server.WaitPost(() => CrossTrigger(SPlayer, MapData.Grid.Owner, rampPosition, Vector2.UnitY, forward: true));

        await Server.WaitAssertion(() =>
        {
            var xform = SEntMan.GetComponent<TransformComponent>(SPlayer);
            Assert.Multiple(() =>
            {
                Assert.That(xform.MapUid, Is.EqualTo(upperMap));
                Assert.That(xform.GridUid, Is.Null);
            });
        });
    }

    [Test]
    public async Task RampPhysicalMovementTriggersPortalBeforeBlocker()
    {
        EntityUid upperMap = default;
        EntityUid upperGrid = default;
        var rampPosition = new Vector2(1.5f, 1.5f);

        await Server.WaitPost(() =>
        {
            (upperMap, upperGrid) = CreateLinkedStack();
            FillGrid(MapData.Grid.Owner);
            PlacePlayer(MapData.Grid.Owner, rampPosition - new Vector2(0f, 0.75f));
            SEntMan.SpawnAttachedTo(
                "ZLevelRampUp",
                new EntityCoordinates(MapData.Grid.Owner, rampPosition));
        });
        await RunUntilSynced();

        await Move(DirectionFlag.North, 3f);

        await Server.WaitAssertion(() =>
        {
            var xform = SEntMan.GetComponent<TransformComponent>(SPlayer);
            Assert.Multiple(() =>
            {
                Assert.That(xform.MapUid, Is.EqualTo(upperMap));
                Assert.That(xform.GridUid, Is.EqualTo(upperGrid));
            });
        });
    }

    [Test]
    public async Task RampPortalFailsWithoutMovingWhenDestinationIsBlocked()
    {
        EntityUid lowerRamp = default;
        EntityUid upperGrid = default;
        var rampPosition = new Vector2(0.5f, 0.5f);

        await Server.WaitPost(() =>
        {
            (_, upperGrid) = CreateLinkedStack();
            PlacePlayer(MapData.Grid.Owner, rampPosition);
            lowerRamp = SEntMan.SpawnAttachedTo(
                "ZLevelRampUp",
                new EntityCoordinates(MapData.Grid.Owner, rampPosition));
        });
        await RunTicks(4);

        await Server.WaitPost(() =>
        {
            SEntMan.SpawnAttachedTo(WallPrototype, new EntityCoordinates(upperGrid, rampPosition));
        });
        await RunTicks(2);

        await Server.WaitPost(() =>
        {
            var portals = SEntMan.System<ZLevelPortalSystem>();
            var portal = SEntMan.GetComponent<ZLevelPortalComponent>(lowerRamp);
            var portalEntity = (lowerRamp, (ZLevelPortalComponent?) portal);
            Assert.That(portals.CanTraverseZ(SPlayer, portalEntity, 1), Is.False);
            Assert.That(portals.TryTraverseZ(SPlayer, portalEntity, 1), Is.False);

            var xform = SEntMan.GetComponent<TransformComponent>(SPlayer);
            Assert.Multiple(() =>
            {
                Assert.That(xform.MapUid, Is.EqualTo(MapData.MapUid));
                Assert.That(xform.GridUid, Is.EqualTo(MapData.Grid.Owner));
            });
        });
    }

    [Test]
    public async Task RampHardGateStopsMovementWhenPortalCannotTraverse()
    {
        var rampPosition = new Vector2(1.5f, 0.5f);

        await Server.WaitPost(() =>
        {
            FillGrid(MapData.Grid.Owner);
            PlacePlayer(MapData.Grid.Owner, rampPosition - new Vector2(0f, 0.3f));
            SEntMan.SpawnAttachedTo(
                "ZLevelRampUp",
                new EntityCoordinates(MapData.Grid.Owner, rampPosition));
        });
        await RunUntilSynced();

        await Move(DirectionFlag.North, 0.8f);

        await Server.WaitAssertion(() =>
        {
            var position = Transform.GetWorldPosition(SPlayer);
            var xform = SEntMan.GetComponent<TransformComponent>(SPlayer);
            Assert.Multiple(() =>
            {
                Assert.That(xform.MapUid, Is.EqualTo(MapData.MapUid));
                Assert.That(position.Y, Is.LessThan(rampPosition.Y + 0.2f));
            });
        });
    }

    [Test]
    public async Task RampSlowdownUsesPuddleStyleContactModifier()
    {
        EntityUid ramp = default;
        var rampPosition = new Vector2(0.5f, 0.5f);

        await Server.WaitPost(() =>
        {
            FillGrid(MapData.Grid.Owner);
            PlacePlayer(MapData.Grid.Owner, rampPosition);
            ramp = SEntMan.SpawnAttachedTo(
                "ZLevelRampUp",
                new EntityCoordinates(MapData.Grid.Owner, rampPosition));
        });
        await RunUntilSynced();

        await Server.WaitAssertion(() =>
        {
            var modifier = SEntMan.GetComponent<SpeedModifierContactsComponent>(ramp);
            Assert.Multiple(() =>
            {
                Assert.That(modifier.WalkSpeedModifier, Is.LessThan(1f));
                Assert.That(modifier.SprintSpeedModifier, Is.LessThan(1f));
            });
        });
    }

    [Test]
    public async Task ReversingBeforeRampTriggerDoesNotTraverse()
    {
        EntityUid upperMap = default;
        var rampPosition = new Vector2(0.5f, 0.5f);

        await Server.WaitPost(() =>
        {
            (upperMap, _) = CreateLinkedStack();
            PlacePlayer(MapData.Grid.Owner, rampPosition - new Vector2(0f, 0.2f));
            SEntMan.SpawnAttachedTo(
                "ZLevelRampUp",
                new EntityCoordinates(MapData.Grid.Owner, rampPosition));
        });
        await RunTicks(4);

        await Server.WaitPost(() =>
        {
            Transform.SetCoordinates(SPlayer, new EntityCoordinates(MapData.Grid.Owner, rampPosition + new Vector2(0f, 0.03f)));
            Transform.SetCoordinates(SPlayer, new EntityCoordinates(MapData.Grid.Owner, rampPosition - new Vector2(0f, 0.2f)));
        });

        await Server.WaitAssertion(() =>
        {
            var xform = SEntMan.GetComponent<TransformComponent>(SPlayer);
            Assert.Multiple(() =>
            {
                Assert.That(xform.MapUid, Is.Not.EqualTo(upperMap));
                Assert.That(xform.MapUid, Is.EqualTo(MapData.MapUid));
            });
        });
    }

    [Test]
    public async Task FailedRampTriggerLatchesUntilBodyRetreats()
    {
        EntityUid upperGrid = default;
        EntityUid lowerRamp = default;
        EntityUid blocker = default;
        var rampPosition = new Vector2(0.5f, 0.5f);

        await Server.WaitPost(() =>
        {
            (_, upperGrid) = CreateLinkedStack();
            PlacePlayer(MapData.Grid.Owner, rampPosition - new Vector2(0f, 0.2f));
            lowerRamp = SEntMan.SpawnAttachedTo(
                "ZLevelRampUp",
                new EntityCoordinates(MapData.Grid.Owner, rampPosition));
        });
        await RunTicks(4);

        await Server.WaitPost(() =>
        {
            blocker = SEntMan.SpawnAttachedTo(WallPrototype, new EntityCoordinates(upperGrid, rampPosition));
            CrossTrigger(SPlayer, MapData.Grid.Owner, rampPosition, Vector2.UnitY, forward: true);
        });
        await RunTicks(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<TransformComponent>(SPlayer).MapUid, Is.EqualTo(MapData.MapUid));
        });

        await Server.WaitPost(() =>
        {
            SEntMan.DeleteEntity(blocker);
            Transform.SetCoordinates(SPlayer, new EntityCoordinates(MapData.Grid.Owner, rampPosition + new Vector2(0f, 0.02f)));
            Transform.SetCoordinates(SPlayer, new EntityCoordinates(MapData.Grid.Owner, rampPosition + new Vector2(0f, 0.1f)));
        });
        await RunTicks(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<TransformComponent>(SPlayer).MapUid, Is.EqualTo(MapData.MapUid),
                "a failed ramp trigger must not fire again while the body jitters near the sensor");
        });

        await Server.WaitPost(() =>
        {
            Transform.SetCoordinates(SPlayer, new EntityCoordinates(MapData.Grid.Owner, rampPosition - new Vector2(0f, 0.2f)));
            CrossTrigger(SPlayer, MapData.Grid.Owner, rampPosition, Vector2.UnitY, forward: true);
        });

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<TransformComponent>(SPlayer).GridUid, Is.EqualTo(upperGrid));
        });
    }

    private void AssertRampPrototype(EntityUid uid, int expectedOffset)
    {
        var trigger = SEntMan.GetComponent<ZLevelPortalTriggerComponent>(uid);
        var portal = SEntMan.GetComponent<ZLevelPortalComponent>(uid);
        var fixtures = SEntMan.GetComponent<FixturesComponent>(uid);
        var transition = fixtures.Fixtures[trigger.TransitionFixture];
        var slowdown = fixtures.Fixtures["zLevelSlowdown"];
        var exitGate = fixtures.Fixtures["zLevelExitGate"];
        var transitionBounds = GetFixtureLocalBounds(transition);
        var slowdownBounds = GetFixtureLocalBounds(slowdown);
        var exitGateBounds = GetFixtureLocalBounds(exitGate);

        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.HasComponent<ZLevelHighGroundComponent>(uid), Is.False);
            Assert.That(SEntMan.HasComponent<SpeedModifierContactsComponent>(uid), Is.True);
            Assert.That(portal.DestinationOffset, Is.EqualTo(expectedOffset));
            Assert.That(fixtures.Fixtures, Has.Count.EqualTo(3));
            Assert.That(transition.Hard, Is.False);
            Assert.That(transitionBounds.Height, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(slowdown.Hard, Is.False);
            Assert.That(slowdownBounds.Left, Is.EqualTo(Box2.UnitCentered.Left).Within(0.001f));
            Assert.That(slowdownBounds.Right, Is.EqualTo(Box2.UnitCentered.Right).Within(0.001f));
            Assert.That(slowdownBounds.Bottom, Is.EqualTo(Box2.UnitCentered.Bottom).Within(0.001f));
            Assert.That(slowdownBounds.Top, Is.EqualTo(Box2.UnitCentered.Top).Within(0.001f));
            Assert.That(exitGate.Hard, Is.True);
            Assert.That(exitGateBounds.Height, Is.EqualTo(0.25f).Within(0.001f));
        });
    }

    private static Box2 GetFixtureLocalBounds(Fixture fixture)
    {
        if (fixture.Shape is PhysShapeAabb aabb)
            return aabb.LocalBounds;

        if (fixture.Shape is PolygonShape polygon)
        {
            var min = polygon.Vertices[0];
            var max = min;
            for (var i = 1; i < polygon.VertexCount; i++)
            {
                min = Vector2.Min(min, polygon.Vertices[i]);
                max = Vector2.Max(max, polygon.Vertices[i]);
            }

            return new Box2(min, max);
        }

        Assert.Fail($"Unsupported ramp fixture shape: {fixture.Shape.GetType()}");
        return default;
    }

    private void CrossTrigger(EntityUid body, EntityUid parent, Vector2 center, Vector2 heading, bool forward)
    {
        var start = center + (forward ? -heading : heading) * 0.2f;
        var end = center + (forward ? heading : -heading) * 0.1f;
        Transform.SetCoordinates(body, new EntityCoordinates(parent, start));
        Transform.SetCoordinates(body, new EntityCoordinates(parent, end));
    }

    private void PlacePlayer(EntityUid parent, Vector2 localPosition)
    {
        Transform.SetCoordinates(SPlayer, new EntityCoordinates(parent, localPosition));
        var vertical = SEntMan.EnsureComponent<ZLevelPhysicsComponent>(SPlayer);
        vertical.VelocityGravity = false;
        SEntMan.System<ZLevelPhysicsSystem>().SetZPosition((SPlayer, vertical), 0f);
    }

    private (EntityUid Map, EntityUid Grid) CreateLinkedStack()
    {
        var upperMap = CreateUnlinkedUpperMap();
        var upperMapComp = SEntMan.GetComponent<MapComponent>(upperMap);
        var upperGrid = MapSystem.CreateGridEntity(upperMapComp.MapId);
        Assert.That(SEntMan.System<ZLevelSystem>().TryLinkGrids(MapData.Grid.Owner, upperGrid), Is.True);
        FillGrid(upperGrid);
        return (upperMap, upperGrid);
    }

    private EntityUid CreateUnlinkedUpperMap()
    {
        var upperMap = MapSystem.CreateMap(out _);
        Assert.That(SEntMan.System<ZLevelSystem>().TryCreateMapNetwork([MapData.MapUid, upperMap], out _), Is.True);
        return upperMap;
    }

    private void FillGrid(EntityUid grid)
    {
        var tile = new Tile(TileMan[Plating].TileId);
        var gridComp = SEntMan.GetComponent<MapGridComponent>(grid);
        for (var x = -Tiles; x <= Tiles; x++)
        {
            for (var y = -Tiles; y <= Tiles; y++)
            {
                MapSystem.SetTile(grid, gridComp, new Vector2i(x, y), tile);
            }
        }
    }
}
