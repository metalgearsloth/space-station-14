using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Input;
using Robust.Client.Input;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests.Hands;

[TestFixture]
public sealed class HandTests : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestPickUpThenDropInContainerTestBox
  name: box
  components:
  - type: EntityStorage
  - type: ContainerContainer
    containers:
      entity_storage: !type:Container
";


    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        DummyTicker = false
    };

    [Test]
    public async Task TestPickupDrop()
    {
        var pair = Pair;
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var mapSystem = server.System<SharedMapSystem>();
        var sys = entMan.System<SharedHandsSystem>();
        var tSys = entMan.System<TransformSystem>();

        var data = await pair.CreateTestMap();
        await pair.RunTicksSync(5);

        EntityUid item = default;
        EntityUid player = default;
        HandsComponent hands = default!;
        await server.WaitPost(() =>
        {
            player = playerMan.Sessions.First().AttachedEntity!.Value;
            var xform = entMan.GetComponent<TransformComponent>(player);
            item = entMan.SpawnEntity("Crowbar", tSys.GetMapCoordinates(player, xform: xform));
            hands = entMan.GetComponent<HandsComponent>(player);
            sys.TryPickup(player, item, hands.ActiveHandId!);
        });

        // run ticks here is important, as errors may happen within the container system's frame update methods.
        await pair.RunTicksSync(5);
        Assert.That(sys.GetActiveItem((player, hands)), Is.EqualTo(item));

        await server.WaitPost(() =>
        {
            sys.TryDrop(player, item);
        });

        await pair.RunTicksSync(5);
        Assert.That(sys.GetActiveItem((player, hands)), Is.Null);

        await server.WaitPost(() => mapSystem.DeleteMap(data.MapId));
    }

    [Test]
    public async Task RapidCrowbarPickupDropDoesNotReuseFallVelocity()
    {
        var pair = Pair;
        var server = pair.Server;
        var entMan = server.EntMan;
        var mapSystem = server.System<SharedMapSystem>();
        var handsSystem = server.System<SharedHandsSystem>();
        var transforms = server.System<TransformSystem>();
        var zLevels = server.System<ZLevelSystem>();
        var zPhysics = server.System<ZLevelPhysicsSystem>();
        var data = await pair.CreateTestMap();
        await pair.RunTicksSync(5);

        EntityUid lowerMap = default;
        EntityUid playerMap = default;
        EntityUid crowbar = default;
        EntityUid player = default;
        await server.WaitPost(() =>
        {
            lowerMap = mapSystem.CreateMap(out _);
            player = server.PlayerMan.Sessions.First().AttachedEntity!.Value;
            playerMap = transforms.GetMap(player)!.Value;
            Assert.That(zLevels.TryCreateMapNetwork([lowerMap, playerMap], out _), Is.True);

            crowbar = entMan.SpawnEntity("Crowbar", transforms.GetMapCoordinates(player));
            var vertical = entMan.GetComponent<ZLevelPhysicsComponent>(crowbar);
            var presentation = entMan.GetComponent<ZLevelPresentationComponent>(crowbar);

            zPhysics.SetZPosition((crowbar, vertical), 0.4f);
            zPhysics.SetZVelocity((crowbar, vertical), -6f);
            Assert.That(handsSystem.TryPickupAnyHand(player, crowbar), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(vertical.Velocity, Is.Zero);
                Assert.That(presentation.LocalHeight, Is.Zero.Within(0.0001f));
            });

            Assert.That(handsSystem.TryDrop(player, crowbar), Is.True);
            Assert.That(vertical.Velocity, Is.Zero);

            zPhysics.SetZPosition((crowbar, vertical), 0.2f);
            zPhysics.SetZVelocity((crowbar, vertical), -3f);
            Assert.That(handsSystem.TryPickupAnyHand(player, crowbar), Is.True);
            Assert.That(handsSystem.TryDrop(player, crowbar), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(vertical.Velocity, Is.Zero, "the second drop reused velocity from the first fall");
                Assert.That(presentation.LocalHeight, Is.Zero.Within(0.0001f));
                Assert.That(transforms.GetMap(crowbar), Is.EqualTo(playerMap));
            });
        });

        await pair.RunTicksSync(5);
        await server.WaitPost(() =>
        {
            var vertical = entMan.GetComponent<ZLevelPhysicsComponent>(crowbar);
            var presentation = entMan.GetComponent<ZLevelPresentationComponent>(crowbar);
            Assert.Multiple(() =>
            {
                Assert.That(vertical.Velocity, Is.Zero);
                Assert.That(presentation.LocalHeight, Is.Zero.Within(0.0001f));
            });

            mapSystem.DeleteMap(entMan.GetComponent<MapComponent>(lowerMap).MapId);
            mapSystem.DeleteMap(data.MapId);
        });
    }

    [Test]
    [EnsureCVar(Side.Client,
        typeof(Robust.Shared.CVars),
        nameof(Robust.Shared.CVars.NetInterpCorrectionHalfLife),
        10f)]
    public async Task ClientTryDropSnapsAfterTargetPlacement()
    {
        var pair = Pair;
        var server = pair.Server;
        var client = pair.Client;
        var data = await pair.CreateTestMap();
        await pair.RunTicksSync(5);

        EntityUid serverItem = default;
        EntityUid serverPlayer = default;
        NetEntity netItem = default;
        NetEntity netPlayer = default;
        await server.WaitPost(() =>
        {
            serverPlayer = server.PlayerMan.Sessions.First().AttachedEntity!.Value;
            var hands = server.EntMan.GetComponent<HandsComponent>(serverPlayer);
            var transforms = server.System<TransformSystem>();
            serverItem = server.EntMan.SpawnEntity("Crowbar", transforms.GetMapCoordinates(serverPlayer));
            Assert.That(server.System<SharedHandsSystem>()
                .TryPickup(serverPlayer, serverItem, hands.ActiveHandId!), Is.True);
            netItem = server.EntMan.GetNetEntity(serverItem);
            netPlayer = server.EntMan.GetNetEntity(serverPlayer);
        });

        await pair.RunTicksSync(5);

        EntityCoordinates clientTarget = default;
        await client.WaitAssertion(() =>
        {
            var player = client.EntMan.GetEntity(netPlayer);
            var item = client.EntMan.GetEntity(netItem);
            var transforms = client.System<Robust.Client.GameObjects.TransformSystem>();
            var playerXform = client.EntMan.GetComponent<TransformComponent>(player);
            clientTarget = playerXform.Coordinates.Offset(Vector2.UnitX);
            var inputManager = client.ResolveDependency<IInputManager>();
            var inputSystem = client.System<Robust.Client.GameObjects.InputSystem>();
            var players = client.ResolveDependency<Robust.Client.Player.IPlayerManager>();
            var functionId = inputManager.NetworkBindMap.KeyFunctionID(ContentKeyFunctions.Drop);
            var message = new ClientFullInputCmdMessage(client.Timing.CurTick, client.Timing.TickFraction, functionId)
            {
                State = BoundKeyState.Down,
                Coordinates = clientTarget,
            };

            inputSystem.HandleInputCommand(players.LocalSession, ContentKeyFunctions.Drop, message);
            Assert.Multiple(() =>
            {
                Assert.That(transforms.TryGetRenderPoseDebugData(item, out _), Is.False);
                Assert.That(transforms.GetRenderWorldPosition(item), Is.EqualTo(transforms.GetWorldPosition(item)));
            });
        });

        await pair.RunTicksSync(4);

        await server.WaitAssertion(() =>
        {
            var hands = server.EntMan.GetComponent<HandsComponent>(serverPlayer);
            Assert.That(server.System<SharedHandsSystem>().GetActiveItem((serverPlayer, hands)), Is.Null);
        });

        await client.WaitAssertion(() =>
        {
            var item = client.EntMan.GetEntity(netItem);
            var transforms = client.System<Robust.Client.GameObjects.TransformSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(transforms.TryGetRenderPoseDebugData(item, out _), Is.False);
                Assert.That(transforms.GetRenderWorldPosition(item), Is.EqualTo(transforms.GetWorldPosition(item)));
            });
        });

        await client.WaitPost(() =>
        {
            var inputManager = client.ResolveDependency<IInputManager>();
            var inputSystem = client.System<Robust.Client.GameObjects.InputSystem>();
            var players = client.ResolveDependency<Robust.Client.Player.IPlayerManager>();
            var functionId = inputManager.NetworkBindMap.KeyFunctionID(ContentKeyFunctions.Drop);
            var message = new ClientFullInputCmdMessage(client.Timing.CurTick, client.Timing.TickFraction, functionId)
            {
                State = BoundKeyState.Up,
                Coordinates = clientTarget,
            };

            inputSystem.HandleInputCommand(players.LocalSession, ContentKeyFunctions.Drop, message);
        });

        await pair.RunTicksSync(1);
        await server.WaitPost(() => server.System<SharedMapSystem>().DeleteMap(data.MapId));
    }

    [Test]
    public async Task TestPickUpThenDropInContainer()
    {
        var pair = Pair;
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await pair.RunTicksSync(5);

        var entMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var mapSystem = server.System<SharedMapSystem>();
        var sys = entMan.System<SharedHandsSystem>();
        var tSys = entMan.System<TransformSystem>();
        var containerSystem = server.System<SharedContainerSystem>();

        EntityUid item = default;
        EntityUid box = default;
        EntityUid player = default;
        HandsComponent hands = default!;

        // spawn the elusive box and crowbar at the coordinates
        await server.WaitPost(() => box = server.EntMan.SpawnEntity("TestPickUpThenDropInContainerTestBox", map.GridCoords));
        await server.WaitPost(() => item = server.EntMan.SpawnEntity("Crowbar", map.GridCoords));
        // place the player at the exact same coordinates and have them grab the crowbar
        await server.WaitPost(() =>
        {
            player = playerMan.Sessions.First().AttachedEntity!.Value;
            tSys.PlaceNextTo(player, item);
            hands = entMan.GetComponent<HandsComponent>(player);
            sys.TryPickup(player, item, hands.ActiveHandId!);
        });
        await pair.RunTicksSync(5);
        Assert.That(sys.GetActiveItem((player, hands)), Is.EqualTo(item));

        // Open then close the box to place the player, who is holding the crowbar, inside of it
        var storage = server.System<EntityStorageSystem>();
        await server.WaitPost(() =>
        {
            storage.OpenStorage(box);
            storage.CloseStorage(box);
        });
        await pair.RunTicksSync(5);
        Assert.That(containerSystem.IsEntityInContainer(player), Is.True);

        // Dropping the item while the player is inside the box should cause the item
        // to also be inside the same container the player is in now,
        // with the item not being in the player's hands
        await server.WaitPost(() =>
        {
            sys.TryDrop(player, item);
        });
        await pair.RunTicksSync(5);
        var xform = entMan.GetComponent<TransformComponent>(player);
        var itemXform = entMan.GetComponent<TransformComponent>(item);
        Assert.That(sys.GetActiveItem((player, hands)), Is.Not.EqualTo(item));
        Assert.That(containerSystem.IsInSameOrNoContainer((player, xform), (item, itemXform)));

        await server.WaitPost(() => mapSystem.DeleteMap(map.MapId));
    }
}
