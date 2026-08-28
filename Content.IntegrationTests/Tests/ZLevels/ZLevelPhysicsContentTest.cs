#nullable enable
using System.Linq;
using System.Numerics;
using Content.Client.Animations;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Helpers;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Maps;
using Content.Shared.Throwing;
using Content.Shared.ZLevels;
using Robust.Client.GameObjects;
using Robust.Shared;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.ZLevels;

[TestFixture]
[TestOf(typeof(ZLevelPhysicsContentSystem))]
public sealed class ZLevelPhysicsContentTest : InteractionTest
{
    private static readonly ProtoId<GameMapPrototype> DevMapId = "Dev";

    private sealed class LandListenerSystem : TestListenerSystem<LandEvent>;

    [SidedDependency(Side.Server)] private readonly ThrowingSystem _throwing = default!;

    [Test]
    public void DevMapUsesRandomOffsetAndRotation()
    {
        var devMap = SProtoMan.Index(DevMapId);

        Assert.Multiple(() =>
        {
            Assert.That(devMap.MaxRandomOffset, Is.GreaterThan(0f));
            Assert.That(devMap.RandomRotation, Is.True);
        });
    }

    [Test]
    public async Task CeilingImpactDoesNotRaiseLandEvent()
    {
        await Server.WaitPost(() =>
        {
            SEntMan.EnsureComponent<TestListenerComponent>(SPlayer);
            SEntMan.EnsureComponent<ZLevelPhysicsComponent>(SPlayer);
        });
        ClearEvents<LandEvent>(SPlayer);

        await Server.WaitPost(() =>
        {
            var ceiling = new ZLevelImpactEvent(5f, ZLevelImpactSurface.Ceiling);
            SEntMan.EventBus.RaiseLocalEvent(SPlayer, ref ceiling, true);
        });

        AssertEvent<LandEvent>(SPlayer, count: 0);

        await Server.WaitPost(() =>
        {
            var floor = new ZLevelImpactEvent(5f, ZLevelImpactSurface.Floor);
            SEntMan.EventBus.RaiseLocalEvent(SPlayer, ref floor, true);
        });

        var land = GetEvents<LandEvent>(SPlayer).Single();
        Assert.Multiple(() =>
        {
            Assert.That(land.User, Is.Null);
            Assert.That(land.PlaySound, Is.True);
        });
    }

    [Test]
    public async Task ThrownEntitiesDisableZPhysicsAndFloorImpactLandsOnce()
    {
        await AddGravity();

        EntityUid item = default;
        await Server.WaitPost(() =>
        {
            item = SEntMan.SpawnEntity("Pen", MapData.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            SEntMan.EnsureComponent<TestListenerComponent>(item);

            Assert.That(_throwing.TryThrow(
                item,
                new Vector2(4f, 0f),
                baseThrowSpeed: 4f,
                user: SPlayer,
                playSound: false), Is.True);

            var zPhysics = SEntMan.GetComponent<ZLevelPhysicsComponent>(item);
            Assert.Multiple(() =>
            {
                Assert.That(zPhysics.Disabled, Is.True);
                Assert.That(zPhysics.Velocity, Is.Zero);
            });
        });

        ClearEvents<LandEvent>(item);

        await Server.WaitPost(() =>
        {
            var floor = new ZLevelImpactEvent(5f, ZLevelImpactSurface.Floor);
            SEntMan.EventBus.RaiseLocalEvent(item, ref floor, true);
            SEntMan.EventBus.RaiseLocalEvent(item, ref floor, true);
        });

        var events = GetEvents<LandEvent>(item).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(events, Has.Count.EqualTo(1));
            Assert.That(events[0].User, Is.EqualTo(SPlayer));
            Assert.That(events[0].PlaySound, Is.False);
        });
    }

    [Test]
    public async Task StopThrowReenablesZPhysics()
    {
        await AddGravity();

        EntityUid item = default;
        await Server.WaitPost(() =>
        {
            item = SEntMan.SpawnEntity("Pen", MapData.GridCoords.Offset(new Vector2(0.5f, 0.5f)));

            Assert.That(_throwing.TryThrow(
                item,
                new Vector2(4f, 0f),
                baseThrowSpeed: 4f,
                user: SPlayer,
                playSound: false), Is.True);

            Assert.That(SEntMan.GetComponent<ZLevelPhysicsComponent>(item).Disabled, Is.True);

            var thrown = SEntMan.GetComponent<ThrownItemComponent>(item);
            SEntMan.System<ThrownItemSystem>().StopThrow(item, thrown);

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<ZLevelPhysicsComponent>(item).Disabled, Is.False);
                Assert.That(SEntMan.HasComponent<ThrownItemComponent>(item), Is.False);
            });
        });
    }

    [Test]
    public async Task ThrownZPhysicsEntityDoesNotStepLocalPositionAtLowTickrate()
    {
        await OverrideCVar(Side.Server, CVars.NetTickrate, 5);
        await CreateZStack(12);
        await AddGravity();

        EntityUid item = default;
        const float startPosition = 0.25f;
        const float startVelocity = 8f;
        await Server.WaitPost(() =>
        {
            item = SEntMan.SpawnEntity("Pen", MapData.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            var zSystem = SEntMan.System<ZLevelPhysicsSystem>();
            var zPhysics = SEntMan.GetComponent<ZLevelPhysicsComponent>(item);
            zSystem.SetZPosition((item, zPhysics), startPosition);
            zSystem.SetZVelocity((item, zPhysics), startVelocity);

            Assert.That(_throwing.TryThrow(
                item,
                new Vector2(40f, 0f),
                baseThrowSpeed: 20f,
                user: SPlayer,
                playSound: false), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(zPhysics.Disabled, Is.True);
                Assert.That(zPhysics.LocalPosition, Is.EqualTo(startPosition).Within(0.001f));
                Assert.That(zPhysics.Velocity, Is.EqualTo(startVelocity).Within(0.001f));
                Assert.That(SEntMan.GetComponent<PhysicsComponent>(item).BodyStatus, Is.EqualTo(BodyStatus.InAir));
            });
        });

        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var zPhysics = SEntMan.GetComponent<ZLevelPhysicsComponent>(item);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<ThrownItemComponent>(item), Is.True);
                Assert.That(zPhysics.Disabled, Is.True);
                Assert.That(zPhysics.LocalPosition, Is.EqualTo(startPosition).Within(0.001f));
                Assert.That(zPhysics.Velocity, Is.EqualTo(startVelocity).Within(0.001f));
            });
        });
    }

    [Test]
    public async Task NonThrownZPhysicsEntityInterpolatesClientSpriteAtLowTickrate()
    {
        await OverrideCVar(Side.Server, CVars.NetTickrate, 5);
        await CreateZStack(4);
        await AddGravity();

        EntityUid item = default;
        NetEntity netItem = default;
        await Server.WaitPost(() =>
        {
            item = SEntMan.SpawnEntity("Pen", MapData.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            netItem = SEntMan.GetNetEntity(item);
        });

        await RunUntilSynced();

        await Server.WaitPost(() =>
        {
            var zSystem = SEntMan.System<ZLevelPhysicsSystem>();
            var zPhysics = SEntMan.EnsureComponent<ZLevelPhysicsComponent>(item);
            zSystem.SetZPosition((item, zPhysics), 0.8f);
            zSystem.SetZVelocity((item, zPhysics), 8f);
        });

        await Pair.RunTicksSync(3);

        await AssertClientZSpriteInterpolates(netItem);
    }

    [Test]
    public async Task PickupAnimationLerpsSpriteZOffsetToTargetHeight()
    {
        EntityUid item = default;
        NetEntity netItem = default;
        const float itemHeight = 0.75f;
        const float targetHeight = 0.25f;

        await Server.WaitPost(() =>
        {
            item = SEntMan.SpawnEntity("Pen", MapData.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            netItem = SEntMan.GetNetEntity(item);

            var zSystem = SEntMan.System<ZLevelPhysicsSystem>();
            var itemZPhysics = SEntMan.EnsureComponent<ZLevelPhysicsComponent>(item);
            itemZPhysics.Disabled = true;
            zSystem.SetZPosition((item, itemZPhysics), itemHeight, snapRender: true);

            var playerZPhysics = SEntMan.EnsureComponent<ZLevelPhysicsComponent>(SPlayer);
            playerZPhysics.Disabled = true;
            zSystem.SetZPosition((SPlayer, playerZPhysics), targetHeight, snapRender: true);
        });

        await RunUntilSynced();

        await Client.WaitPost(() =>
        {
            var clientItem = CEntMan.GetEntity(netItem);
            var itemXform = CEntMan.GetComponent<TransformComponent>(clientItem);
            var playerXform = CEntMan.GetComponent<TransformComponent>(CPlayer);
            var transforms = CEntMan.System<SharedTransformSystem>();
            var finalMapPos = transforms.ToMapCoordinates(playerXform.Coordinates).Position;
            var finalPos = Vector2.Transform(finalMapPos, transforms.GetInvWorldMatrix(itemXform.Coordinates.EntityId));

            CEntMan.System<EntityPickupAnimationSystem>().AnimateEntityPickup(
                clientItem,
                itemXform.Coordinates,
                finalPos,
                Angle.Zero,
                CPlayer);
        });

        await Client.WaitAssertion(() =>
        {
            var clone = GetClientPickupClone();
            var sprite = CEntMan.GetComponent<SpriteComponent>(clone);
            var (expectedInitial, _) = GetExpectedPickupSpriteOffsets(netItem, itemHeight, targetHeight);

            Assert.Multiple(() =>
            {
                Assert.That(sprite.Offset.X, Is.EqualTo(expectedInitial.X).Within(0.001f));
                Assert.That(sprite.Offset.Y, Is.EqualTo(expectedInitial.Y).Within(0.001f));
            });
        });

        await Client.WaitPost(() => CEntMan.FrameUpdate(0.0625f));

        await Client.WaitAssertion(() =>
        {
            var clone = GetClientPickupClone();
            var sprite = CEntMan.GetComponent<SpriteComponent>(clone);
            var (expectedInitial, expectedFinal) = GetExpectedPickupSpriteOffsets(netItem, itemHeight, targetHeight);

            Assert.Multiple(() =>
            {
                Assert.That(sprite.Offset.X, Is.EqualTo(expectedInitial.X).Within(0.001f));
                Assert.That(sprite.Offset.Y,
                    Is.GreaterThan(MathF.Min(expectedInitial.Y, expectedFinal.Y))
                        .And.LessThan(MathF.Max(expectedInitial.Y, expectedFinal.Y)));
            });
        });
    }

    private async Task CreateZStack(int levels)
    {
        await Server.WaitPost(() =>
        {
            var maps = new EntityUid[levels];
            maps[0] = MapData.MapUid;
            for (var i = 1; i < maps.Length; i++)
                maps[i] = MapSystem.CreateMap(out _);

            Assert.That(SEntMan.System<ZLevelSystem>().TryCreateMapNetwork(maps, out _), Is.True);
        });
    }

    private EntityUid GetClientPickupClone()
    {
        EntityUid? clone = null;
        var query = CEntMan.AllEntityQueryEnumerator<EntityPickupAnimationComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            Assert.That(clone, Is.Null, "Expected only one active pickup animation clone.");
            clone = uid;
        }

        Assert.That(clone, Is.Not.Null, "Expected an active pickup animation clone.");
        return clone.Value;
    }

    private (Vector2 Initial, Vector2 Final) GetExpectedPickupSpriteOffsets(
        NetEntity netItem,
        float itemHeight,
        float targetHeight)
    {
        var clientItem = CEntMan.GetEntity(netItem);
        var zVisuals = CEntMan.System<ZLevelPhysicsVisualSystem>();
        var sourceSprite = CEntMan.GetComponent<SpriteComponent>(clientItem);
        var baseOffset = zVisuals.GetBaseSpriteOffset(clientItem, sourceSprite);
        var verticalOffset = Client.CfgMan.GetCVar(CVars.RenderZLevelVerticalOffset);

        return (
            baseOffset + new Vector2(0f, itemHeight * verticalOffset),
            baseOffset + new Vector2(0f, targetHeight * verticalOffset));
    }

    private async Task AssertClientZSpriteInterpolates(NetEntity netItem)
    {
        await Client.WaitAssertion(() =>
        {
            var clientItem = CEntMan.GetEntity(netItem);
            var xform = CEntMan.GetComponent<TransformComponent>(clientItem);
            var zPhysics = CEntMan.GetComponent<ZLevelPhysicsComponent>(clientItem);
            var physics = CEntMan.GetComponent<PhysicsComponent>(clientItem);
            var meta = CEntMan.GetComponent<MetaDataComponent>(clientItem);
            var containers = CEntMan.System<SharedContainerSystem>();
            var zSystem = CEntMan.System<ZLevelPhysicsSystem>();
            var visuals = CEntMan.System<ZLevelPhysicsVisualSystem>();
            var transforms = CEntMan.System<SharedTransformSystem>();
            var sprite = CEntMan.GetComponent<SpriteComponent>(clientItem);

            CTiming.TickRemainder = CTiming.TickPeriod / 2;
            var renderPosition = visuals.GetRenderPosition(clientItem, zPhysics);
            var renderPrevious = zPhysics.RenderPreviousAbsolutePosition - zPhysics.CurrentZLevel;
            var renderTarget = zPhysics.RenderTargetAbsolutePosition - zPhysics.CurrentZLevel;
            var renderMin = MathF.Min(renderPrevious, renderTarget);
            var renderMax = MathF.Max(renderPrevious, renderTarget);
            var verticalOffset = Client.CfgMan.GetCVar(CVars.RenderZLevelVerticalOffset);
            var worldRotation = transforms.GetWorldRotation(xform);
            var expectedLocalZOffset = ZLevelPhysicsVisualSystem.GetLocalRenderOffset(
                renderPosition,
                verticalOffset,
                worldRotation,
                sprite.NoRotation);
            var minLocalZOffset = ZLevelPhysicsVisualSystem.GetLocalRenderOffset(
                renderMin,
                verticalOffset,
                worldRotation,
                sprite.NoRotation);
            var maxLocalZOffset = ZLevelPhysicsVisualSystem.GetLocalRenderOffset(
                renderMax,
                verticalOffset,
                worldRotation,
                sprite.NoRotation);

            CEntMan.FrameUpdate((float) CTiming.TickPeriod.TotalSeconds);
            var expectedSpriteOffset = zPhysics.SpriteOffsetDefault + expectedLocalZOffset;
            var spriteOffsetMin = zPhysics.SpriteOffsetDefault + minLocalZOffset;
            var spriteOffsetMax = zPhysics.SpriteOffsetDefault + maxLocalZOffset;

            Assert.Multiple(() =>
            {
                Assert.That(zPhysics.Velocity, Is.Not.EqualTo(0f),
                    $"client z velocity {zPhysics.Velocity}, z local {zPhysics.LocalPosition}, body status {physics.BodyStatus}");
                Assert.That(zPhysics.RenderStateInitialized, Is.True);
                Assert.That(zSystem.ActiveBodies, Does.Contain(clientItem),
                    $"client active z bodies missing item; z velocity {zPhysics.Velocity}, local {zPhysics.LocalPosition}, parent {xform.ParentUid}, grid {xform.GridUid}, map {xform.MapUid}, z map {CEntMan.HasComponent<ZLevelMapComponent>(xform.MapUid)}, body type {physics.BodyType}, body status {physics.BodyStatus}, anchored {xform.Anchored}, meta flags {meta.Flags}, in container {containers.IsEntityOrParentInContainer(clientItem, meta, xform)}");
                Assert.That(MathF.Abs(zPhysics.RenderTargetAbsolutePosition - zPhysics.RenderPreviousAbsolutePosition),
                    Is.GreaterThan(0.001f),
                    $"client z render prev {zPhysics.RenderPreviousAbsolutePosition}, target {zPhysics.RenderTargetAbsolutePosition}, tick {zPhysics.RenderLerpTick}, current {CTiming.CurTick}");
                Assert.That(renderPosition, Is.GreaterThan(renderMin).And.LessThan(renderMax),
                    $"client z render {renderPosition}, prev {zPhysics.RenderPreviousAbsolutePosition}, target {zPhysics.RenderTargetAbsolutePosition}, tick {zPhysics.RenderLerpTick}, current {CTiming.CurTick}, local {zPhysics.LocalPosition}");
                Assert.That(zPhysics.SpriteOffsetDefaultCached, Is.True);
                Assert.That(sprite.Offset.Y, Is.GreaterThan(MathF.Min(spriteOffsetMin.Y, spriteOffsetMax.Y)).And.LessThan(MathF.Max(spriteOffsetMin.Y, spriteOffsetMax.Y)),
                    $"client sprite offset {sprite.Offset}, expected {expectedSpriteOffset}, render {renderPosition}, prev {zPhysics.RenderPreviousAbsolutePosition}, target {zPhysics.RenderTargetAbsolutePosition}");
                Assert.That(sprite.Offset.X, Is.EqualTo(expectedSpriteOffset.X).Within(0.001f));
                Assert.That(sprite.Offset.Y, Is.EqualTo(expectedSpriteOffset.Y).Within(0.001f));
            });
        });
    }
}
