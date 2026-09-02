#nullable enable
using System.Numerics;
using System.Collections.Generic;
using Content.Client.ZLevels;
using Content.IntegrationTests.Fixtures;
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
public sealed class ZLevelSurfacePresentationTest : GameTest
{
    [TestCase(0f)]
    [TestCase(0.7f)]
    public async Task WallTopFootAndPickingFootprintShareProjection(float projectionY)
    {
        var mapIds = new MapId[2];

        await Client.WaitAssertion(() =>
        {
            var entities = CEntMan;
            var maps = entities.System<SharedMapSystem>();
            var transforms = entities.System<TransformSystem>();
            var zLevels = entities.System<ZLevelSystem>();
            var presentation = entities.System<ZLevelPresentationSystem>();
            var surfaces = entities.System<ZLevelSurfaceProjectionSystem>();
            var mapUids = new EntityUid[2];

            for (var i = 0; i < mapUids.Length; i++)
                mapUids[i] = maps.CreateMap(out mapIds[i]);

            var networkUid = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var networkComp = entities.AddComponent<ZLevelMapNetworkComponent>(networkUid);
            Assert.That(zLevels.TryAddMaps((networkUid, networkComp), new Dictionary<EntityUid, int>
            {
                [mapUids[0]] = 0,
                [mapUids[1]] = 1,
            }), Is.True);
            zLevels.SetProjectionOffset(networkUid, new Vector2(0f, projectionY));

            var lowerGrid = maps.CreateGridEntity(mapIds[0]);
            var upperGrid = maps.CreateGridEntity(mapIds[1]);
            maps.SetTile(lowerGrid, Vector2i.Zero, new Tile(1));
            maps.SetTile(upperGrid, Vector2i.Zero, new Tile(1));
            SetGridPose(transforms, lowerGrid, new Vector2(4f, 3f), Angle.FromDegrees(31f));
            SetGridPose(transforms, upperGrid, new Vector2(4f, 3f), Angle.FromDegrees(31f));

            var localContact = new Vector2(0.5f, 0.5f);
            var wall = entities.SpawnEntity("WallSolid", new EntityCoordinates(lowerGrid, localContact));
            var wallGround = entities.GetComponent<ZLevelHighGroundComponent>(wall);
            var wallFixtures = entities.GetComponent<FixturesComponent>(wall);
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<ZLevelTopSurfaceVisualComponent>(wall), Is.True,
                    "ordinary content walls must opt into a real safe-top presentation");
                Assert.That(wallGround.SolidVolume, Is.True,
                    "the visual wall top must retain its solid volume");
                Assert.That(wallGround.Height, Is.EqualTo(1.05f).Within(0.001f),
                    "wall tops must expose one constant authored support height");
                Assert.That(wallFixtures.Fixtures.ContainsKey(wallGround.SurfaceFixture), Is.True,
                    "wall tops must use one explicit authored support fixture");
            });

            var feet = entities.SpawnEntity(null, new EntityCoordinates(upperGrid, localContact));
            var feetPresentation = entities.AddComponent<ZLevelPresentationComponent>(feet);
            var feetPhysics = entities.AddComponent<ZLevelPhysicsComponent>(feet);
            presentation.SetLocalHeight((feet, feetPresentation), 0.05f);
            transforms.SnapRenderPose(feet);
            feetPhysics.SupportProvider = wall;
            feetPhysics.SupportSurface = ZLevelSupportSurface.HighGround;
            feetPhysics.SupportHeight = 1.05f;
            feetPhysics.GroundState = ZLevelGroundState.Grounded;

            var polygon = new Vector2[4];
            Assert.That(surfaces.TryGetProjectedSurface(
                (wall, null),
                mapUids[1],
                polygon,
                out var count,
                out var averageHeight), Is.True);
            Assert.That(surfaces.TryGetSurfaceLayer(wall, averageHeight, out var surfaceLayer), Is.True);

            var centroid = Vector2.Zero;
            for (var i = 0; i < count; i++)
                centroid += polygon[i];
            centroid /= count;

            var renderedFeet = transforms.GetRenderWorldPoseForLayer(feet, mapUids[1]);
            Assert.Multiple(() =>
            {
                Assert.That(surfaceLayer, Is.EqualTo(mapUids[1]));
                Assert.That(averageHeight, Is.EqualTo(1.05f).Within(0.001f));
                AssertVector(centroid, renderedFeet.Position,
                    "the rendered foot/contact point diverged from the projected safe top");
                Assert.That(ZLevelSurfaceProjectionSystem.ContainsPoint(
                    polygon.AsSpan(0, count),
                    renderedFeet.Position), Is.True);
                Assert.That(ZLevelSurfaceProjectionSystem.ContainsPoint(
                    polygon.AsSpan(0, count),
                    renderedFeet.Position + new Vector2(2f, 0f)), Is.False);
            });

            var predictedLocal = localContact + new Vector2(0.1f, 0f);
            transforms.SetLocalPositionNoLerp(feet, predictedLocal);
            entities.System<ZLevelPhysicsSystem>().Update(1f);
            var predictedFeet = transforms.GetRenderWorldPoseForLayer(feet, mapUids[1]);
            Assert.Multiple(() =>
            {
                Assert.That(entities.GetComponent<TransformComponent>(feet).LocalPosition, Is.EqualTo(predictedLocal));
                Assert.That(feetPresentation.LocalHeight, Is.EqualTo(0.05f));
                Assert.That(feetPhysics.GroundState, Is.EqualTo(ZLevelGroundState.Grounded));
                Assert.That(feetPhysics.SupportProvider, Is.EqualTo(wall));
                Assert.That(ZLevelSurfaceProjectionSystem.ContainsPoint(
                    polygon.AsSpan(0, count),
                    predictedFeet.Position), Is.True,
                    "ordinary predicted roof XY movement was not retained inside the shared support footprint");
            });

            maps.DeleteMap(mapIds[1]);
            maps.DeleteMap(mapIds[0]);
            if (entities.EntityExists(networkUid))
                entities.DeleteEntity(networkUid);
        });
    }

    [Test]
    public void ProjectedPickingPrefersHeightAndHasStableFinalTie()
    {
        var low = new ZLevelProjectedPickKey(new EntityUid(10), 1f, 100, 5, 0f);
        var high = new ZLevelProjectedPickKey(new EntityUid(2), 2f, -100, 0, 100f);
        Assert.That(ZLevelProjectedPicking.Compare(high, low), Is.LessThan(0),
            "normal draw depth must not beat a genuinely higher projected level");

        var firstTie = new ZLevelProjectedPickKey(new EntityUid(3), 2f, 10, 1, 0f);
        var secondTie = new ZLevelProjectedPickKey(new EntityUid(7), 2f, 10, 1, 0f);
        var comparison = ZLevelProjectedPicking.Compare(firstTie, secondTie);
        Assert.Multiple(() =>
        {
            Assert.That(comparison, Is.Not.Zero);
            Assert.That(comparison, Is.EqualTo(-ZLevelProjectedPicking.Compare(secondTie, firstTie)));
        });
    }

    private static void SetGridPose(TransformSystem transforms, EntityUid grid, Vector2 position, Angle rotation)
    {
        transforms.SetLocalPositionNoLerp(grid, position);
        transforms.SetLocalRotationNoLerp(grid, rotation);
        transforms.SnapRenderPose(grid);
    }

    private static void AssertVector(Vector2 actual, Vector2 expected, string? message = null)
    {
        Assert.That(actual.X, Is.EqualTo(expected.X).Within(0.001f), message);
        Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(0.001f), message);
    }
}
