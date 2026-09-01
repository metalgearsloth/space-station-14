using System.Collections.Generic;
using System.Numerics;
using Content.Client.Animations;
using Content.Client.Eye;
using Content.IntegrationTests.Fixtures;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.ZLevels;

[TestFixture]
public sealed class EntityPickupPresentationTest : GameTest
{
    [TestCase(0f, false)]
    [TestCase(0.7f, false)]
    [TestCase(0f, true)]
    [TestCase(0.7f, true)]
    public async Task PickupAndStorageTrackMovingAndRotatingEndpointGrids(float verticalOffset, bool throughStorage)
    {
        var client = Pair.Client;
        var maps = new EntityUid[2];
        var mapIds = new MapId[2];
        EntityUid networkUid = default;
        EntityUid sourceGrid = default;
        EntityUid targetGrid = default;
        EntityUid clone = default;
        var sourceLocal = new Vector2(0.5f, -0.25f);
        var targetLocal = new Vector2(-0.25f, 0.75f);
        var authoredOffset = new Vector2(0.2f, -0.15f);

        await client.WaitAssertion(() =>
        {
            var entities = client.EntMan;
            var mapSystem = client.System<SharedMapSystem>();
            var transforms = client.System<TransformSystem>();
            var sprites = client.System<SpriteSystem>();
            var zLevels = client.System<ZLevelSystem>();
            var pickup = client.System<EntityPickupAnimationSystem>();

            for (var i = 0; i < maps.Length; i++)
                maps[i] = mapSystem.CreateMap(out mapIds[i]);

            networkUid = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var networkComp = entities.AddComponent<ZLevelMapNetworkComponent>(networkUid);
            Entity<ZLevelMapNetworkComponent> network = (networkUid, networkComp);
            Assert.That(zLevels.TryAddMaps(network, new Dictionary<EntityUid, int>
            {
                [maps[0]] = 0,
                [maps[1]] = 1,
            }), Is.True);
            zLevels.SetProjectionOffset(networkUid, new Vector2(0f, verticalOffset));

            sourceGrid = mapSystem.CreateGridEntity(mapIds[0]);
            targetGrid = mapSystem.CreateGridEntity(mapIds[1]);
            SetGridPose(transforms, sourceGrid, new Vector2(1f, 2f), Angle.FromDegrees(10));
            SetGridPose(transforms, targetGrid, new Vector2(9f, 3f), Angle.FromDegrees(70));

            var item = entities.SpawnEntity(null, new EntityCoordinates(sourceGrid, sourceLocal));
            var itemSprite = entities.AddComponent<SpriteComponent>(item);
            sprites.SetOffset((item, itemSprite), authoredOffset);
            var target = entities.SpawnEntity(null, new EntityCoordinates(targetGrid, targetLocal));

            if (throughStorage)
            {
                client.System<Content.Client.Storage.Systems.StorageSystem>().PickupAnimation(
                    item,
                    new EntityCoordinates(sourceGrid, sourceLocal),
                    new EntityCoordinates(targetGrid, targetLocal),
                    Angle.FromDegrees(15),
                    target);
            }
            else
            {
                pickup.AnimateEntityPickup(
                    item,
                    new EntityCoordinates(sourceGrid, sourceLocal),
                    new EntityCoordinates(targetGrid, targetLocal),
                    Angle.FromDegrees(15),
                    target);
            }

            var query = entities.EntityQueryEnumerator<EntityPickupAnimationComponent, ZLevelPresentationComponent>();
            Assert.That(query.MoveNext(out clone, out _, out _), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(entities.GetComponent<TransformComponent>(clone).ParentUid, Is.EqualTo(maps[0]),
                    "the clone must not remain parented to the old grid");
                AssertVector(entities.GetComponent<SpriteComponent>(clone).Offset, authoredOffset,
                    "z presentation must not mutate the authored sprite offset");
            });

            // Both endpoint coordinate spaces continue moving after the animation starts.
            SetGridPose(transforms, sourceGrid, new Vector2(2f, 4f), Angle.FromDegrees(35));
            SetGridPose(transforms, targetGrid, new Vector2(8f, 7f), Angle.FromDegrees(-25));
        });

        await client.WaitAssertion(() =>
        {
            var entities = client.EntMan;
            var transforms = client.System<TransformSystem>();
            var pickup = client.System<EntityPickupAnimationSystem>();
            Assert.That(entities.EntityExists(clone), Is.True);
            var animation = entities.GetComponent<EntityPickupAnimationComponent>(clone);
            var presentation = entities.GetComponent<ZLevelPresentationComponent>(clone);
            const float progress = 0.5f;
            pickup.UpdatePresentation(clone, animation, presentation, progress);

            var sourcePose = transforms.GetRenderWorldPose(sourceGrid);
            var targetPose = transforms.GetRenderWorldPose(targetGrid);
            var sourceCanonical = sourcePose.CanonicalPosition + sourcePose.Rotation.RotateVec(sourceLocal);
            var targetCanonical = targetPose.CanonicalPosition + targetPose.Rotation.RotateVec(targetLocal);
            var projectionOffset = new Vector2(0f, verticalOffset);
            var sourceProjected = ZLevelProjection.Project(sourceCanonical, 0f, 0, projectionOffset);
            var targetProjected = ZLevelProjection.Project(targetCanonical, 1f, 0, projectionOffset);
            var expectedPosition = Vector2.Lerp(sourceProjected, targetProjected, progress);
            var expectedAbsoluteZ = MathHelper.Lerp(0f, 1f, progress);
            var clonePose = transforms.GetRenderWorldPose(clone);
            Span<RenderLayerSample> samples = stackalloc RenderLayerSample[2];
            var sampleCount = transforms.GetRenderLayerSamples(clone, samples);
            var lowerSample = samples[0];
            var upperSample = samples[1];

            Assert.Multiple(() =>
            {
                Assert.That(progress, Is.GreaterThan(0f).And.LessThan(1f));
                AssertVector(clonePose.Position, expectedPosition);
                Assert.That(clonePose.AbsoluteZ, Is.EqualTo(expectedAbsoluteZ).Within(0.001f));
                Assert.That(sampleCount, Is.EqualTo(2));
                Assert.That(lowerSample.Opacity + upperSample.Opacity, Is.EqualTo(1f).Within(0.001f));
                Assert.That(lowerSample.Opacity, Is.EqualTo(1f - progress).Within(0.001f));
                Assert.That(upperSample.Opacity, Is.EqualTo(progress).Within(0.001f));
                AssertVector(lowerSample.Position, expectedPosition);
                AssertVector(
                    ZLevelProjection.Reproject(upperSample.Position, 1, 0, projectionOffset),
                    expectedPosition,
                    "both renderer samples must land at the same continuous projected position");
                AssertVector(entities.GetComponent<SpriteComponent>(clone).Offset, authoredOffset);
            });
        });

        await client.WaitPost(() =>
        {
            var mapSystem = client.System<SharedMapSystem>();
            for (var i = 0; i < mapIds.Length; i++)
            {
                if (mapSystem.MapExists(mapIds[i]))
                    mapSystem.DeleteMap(mapIds[i]);
            }

            if (client.EntMan.EntityExists(networkUid))
                client.EntMan.DeleteEntity(networkUid);
        });
    }

    [Test]
    public async Task ContentEyeRotationUsesPresentedGridRotation()
    {
        var client = Pair.Client;
        MapId mapId = default;

        await client.WaitAssertion(() =>
        {
            var entities = client.EntMan;
            var mapSystem = client.System<SharedMapSystem>();
            var transforms = client.System<TransformSystem>();
            var eyeLerping = client.System<EyeLerpingSystem>();
            var map = mapSystem.CreateMap(out mapId);
            var gridEntity = mapSystem.CreateGridEntity(mapId);
            mapSystem.SetTile(gridEntity, new Vector2i(1, 0), new Tile(1));
            var grid = gridEntity.Owner;
            var controlled = entities.SpawnEntity(null, new EntityCoordinates(grid, Vector2.UnitX));
            var eye = entities.AddComponent<EyeComponent>(controlled);

            SetGridPose(transforms, grid, new Vector2(3f, 4f), Angle.FromDegrees(37f));
            eyeLerping.AddEye(controlled, eye);

            Assert.Multiple(() =>
            {
                Assert.That(eye.Eye.Rotation.Degrees, Is.EqualTo(-37f).Within(0.001f));
                Assert.That(
                    transforms.GetRenderWorldRotation(controlled).Degrees + eye.Eye.Rotation.Degrees,
                    Is.EqualTo(0f).Within(0.001f));
            });

            SetGridPose(transforms, grid, new Vector2(5f, 2f), Angle.FromDegrees(-23f));
            eyeLerping.FrameUpdate(0f);

            Assert.Multiple(() =>
            {
                Assert.That(eye.Eye.Rotation.Degrees, Is.EqualTo(23f).Within(0.001f));
                Assert.That(
                    transforms.GetRenderWorldRotation(controlled).Degrees + eye.Eye.Rotation.Degrees,
                    Is.EqualTo(0f).Within(0.001f));
            });
        });

        await client.WaitPost(() =>
        {
            var mapSystem = client.System<SharedMapSystem>();
            if (mapSystem.MapExists(mapId))
                mapSystem.DeleteMap(mapId);
        });
    }

    private static void SetGridPose(TransformSystem transforms, EntityUid grid, Vector2 position, Angle rotation)
    {
        transforms.SetLocalPositionNoLerp(grid, position);
        transforms.SetLocalRotationNoLerp(grid, rotation);
        transforms.SnapRenderPose(grid);
    }

    private static void AssertVector(Vector2 actual, Vector2 expected, string message = null)
    {
        Assert.That(actual.X, Is.EqualTo(expected.X).Within(0.001f), message);
        Assert.That(actual.Y, Is.EqualTo(expected.Y).Within(0.001f), message);
    }
}
