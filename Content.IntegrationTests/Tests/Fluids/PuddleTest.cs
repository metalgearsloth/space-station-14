using Content.IntegrationTests.Fixtures;
using Content.Shared.CCVar;
using Content.Server.Fluids.Components;
using Content.Server.Fluids.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Coordinates;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids;
using Content.Shared.Fluids.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

using ClientPuddleComponent = Content.Client.Fluids.Components.PuddleComponent;
using ServerPuddleComponent = Content.Server.Fluids.Components.PuddleComponent;

namespace Content.IntegrationTests.Tests.Fluids
{
    [TestFixture]
    [TestOf(typeof(ServerPuddleComponent))]
    public sealed class PuddleTest : GameTest
    {
        [Test]
        public async Task TilePuddleTest()
        {
            var pair = Pair;
            var server = pair.Server;

            var testMap = await pair.CreateTestMap();

            var spillSystem = server.System<PuddleSystem>();

            await server.WaitAssertion(() =>
            {
                var solution = new Solution("Water", FixedPoint2.New(20));
                var tile = testMap.Tile;
                var gridUid = tile.GridUid;
                var (x, y) = tile.GridIndices;
                var coordinates = new EntityCoordinates(gridUid, x, y);

                Assert.That(spillSystem.TrySpillAt(coordinates, solution, out _), Is.True);
            });
        }

        [Test]
        public async Task SpaceNoPuddleTest()
        {
            var pair = Pair;
            var server = pair.Server;

            var testMap = await pair.CreateTestMap();
            var grid = testMap.Grid;

            var entitySystemManager = server.ResolveDependency<IEntitySystemManager>();
            var spillSystem = server.System<PuddleSystem>();
            var mapSystem = server.System<SharedMapSystem>();

            // Remove all tiles
            await server.WaitPost(() =>
            {
                var tiles = mapSystem.GetAllTiles(grid.Owner, grid.Comp);
                foreach (var tile in tiles)
                {
                    mapSystem.SetTile(grid, tile.GridIndices, Tile.Empty);
                }
            });

            await pair.RunTicksSync(5);

            await server.WaitAssertion(() =>
            {
                var coordinates = grid.Owner.ToCoordinates();
                var solution = new Solution("Water", FixedPoint2.New(20));

                Assert.That(spillSystem.TrySpillAt(coordinates, solution, out _), Is.False);
            });
        }

        /// <summary>
        /// Asserts that:
        /// - puddles only blend across visually joined puddles
        /// - respond to the graphics CVar
        /// - reset when color appearance is removed, and
        /// - recalculate neighbors when a puddle is deleted.
        /// </summary>
        [Test]
        public async Task PuddleShaderColorsUpdateAndReset()
        {
            var pair = Pair;
            var server = pair.Server;
            var client = pair.Client;

            var testMap = await pair.CreateTestMap();
            var spillSystem = server.System<PuddleSystem>();
            var appearance = server.System<SharedAppearanceSystem>();
            var map = server.System<SharedMapSystem>();
            var clientConfig = client.ResolveDependency<IConfigurationManager>();

            EntityUid leftPuddle = default;
            EntityUid rightPuddle = default;
            var red = Color.Red;
            var green = Color.Green;
            var blue = Color.Blue;

            // Set up two adjacent, visually joined puddles with distinct colors and ensure shaders start enabled.
            await client.WaitPost(() =>
            {
                clientConfig.SetCVar(CCVars.PuddleShaders, true);
            });

            await server.WaitAssertion(() =>
            {
                var tile = testMap.Tile;
                var gridUid = tile.GridUid;
                var indices = tile.GridIndices;
                map.SetTile(testMap.Grid, new Vector2i(indices.X + 1, indices.Y), tile.Tile);

                Assert.That(spillSystem.TrySpillAt(
                    new EntityCoordinates(gridUid, indices.X, indices.Y),
                    new Solution("Water", FixedPoint2.New(20)),
                    out leftPuddle),
                    Is.True);

                Assert.That(spillSystem.TrySpillAt(
                    new EntityCoordinates(gridUid, indices.X + 1, indices.Y),
                    new Solution("Water", FixedPoint2.New(20)),
                    out rightPuddle),
                    Is.True);

                appearance.SetData(leftPuddle, PuddleVisuals.SolutionColor, red);
                appearance.SetData(rightPuddle, PuddleVisuals.SolutionColor, green);
                appearance.SetData(leftPuddle, PuddleVisuals.CurrentVolume, 1f);
                appearance.SetData(rightPuddle, PuddleVisuals.CurrentVolume, 1f);
            });

            await pair.RunUntilSynced();

            var leftNet = server.EntMan.GetNetEntity(leftPuddle);
            var rightNet = server.EntMan.GetNetEntity(rightPuddle);
            var clientLeft = client.EntMan.GetEntity(leftNet);
            var clientRight = client.EntMan.GetEntity(rightNet);

            // Joined puddles should have shaders and should set each other as blend colors.
            await client.WaitAssertion(() =>
            {
                var left = client.EntMan.GetComponent<ClientPuddleComponent>(clientLeft);
                var right = client.EntMan.GetComponent<ClientPuddleComponent>(clientRight);

                Assert.That(left.Shader, Is.Not.Null);
                Assert.That(right.Shader, Is.Not.Null);

                Assert.That(left.SolutionColor, Is.EqualTo(red));
                Assert.That(right.SolutionColor, Is.EqualTo(green));
                Assert.That(left.ShaderColor, Is.EqualTo(red));
                Assert.That(left.EastShaderColor, Is.EqualTo(green));
                Assert.That(right.WestShaderColor, Is.EqualTo(red));
            });

            // A low-volume puddle visually stops joining, so neither side should blend across that seam.
            await server.WaitPost(() =>
            {
                appearance.SetData(rightPuddle, PuddleVisuals.CurrentVolume, SharedPuddleSystem.LowThreshold / 2f);
            });

            await pair.RunUntilSynced();

            await client.WaitAssertion(() =>
            {
                var left = client.EntMan.GetComponent<ClientPuddleComponent>(clientLeft);
                var right = client.EntMan.GetComponent<ClientPuddleComponent>(clientRight);

                Assert.That(left.EastShaderColor, Is.EqualTo(red));
                Assert.That(right.WestShaderColor, Is.EqualTo(green));
            });

            // Restoring joinable volume should restore the cross-puddle blend colors.
            await server.WaitPost(() =>
            {
                appearance.SetData(rightPuddle, PuddleVisuals.CurrentVolume, 1f);
            });

            await pair.RunUntilSynced();

            await client.WaitAssertion(() =>
            {
                var left = client.EntMan.GetComponent<ClientPuddleComponent>(clientLeft);
                var right = client.EntMan.GetComponent<ClientPuddleComponent>(clientRight);

                Assert.That(left.EastShaderColor, Is.EqualTo(green));
                Assert.That(right.WestShaderColor, Is.EqualTo(red));
            });

            // Disabling the graphics CVar should remove shader instances and restore normal per-sprite tinting.
            await client.WaitPost(() =>
            {
                clientConfig.SetCVar(CCVars.PuddleShaders, false);
            });

            await pair.RunUntilSynced();

            await client.WaitAssertion(() =>
            {
                var left = client.EntMan.GetComponent<ClientPuddleComponent>(clientLeft);
                var right = client.EntMan.GetComponent<ClientPuddleComponent>(clientRight);
                var leftSprite = client.EntMan.GetComponent<SpriteComponent>(clientLeft);
                var rightSprite = client.EntMan.GetComponent<SpriteComponent>(clientRight);

                Assert.That(left.Shader, Is.Null);
                Assert.That(right.Shader, Is.Null);
                Assert.That(leftSprite.Color, Is.EqualTo(red.WithAlpha(128)));
                Assert.That(rightSprite.Color, Is.EqualTo(green.WithAlpha(128)));
            });

            // Re-enabling the graphics CVar should recreate shaders and restore blend colors for all puddles.
            await client.WaitPost(() =>
            {
                clientConfig.SetCVar(CCVars.PuddleShaders, true);
            });

            await pair.RunUntilSynced();

            await client.WaitAssertion(() =>
            {
                var left = client.EntMan.GetComponent<ClientPuddleComponent>(clientLeft);
                var right = client.EntMan.GetComponent<ClientPuddleComponent>(clientRight);

                Assert.That(left.Shader, Is.Not.Null);
                Assert.That(right.Shader, Is.Not.Null);
                Assert.That(left.ShaderColor, Is.EqualTo(red));
                Assert.That(left.EastShaderColor, Is.EqualTo(green));
                Assert.That(right.WestShaderColor, Is.EqualTo(red));
            });

            // Removing color appearance should reset the puddle to white and update its neighbor's blend color.
            await server.WaitPost(() =>
            {
                appearance.RemoveData(rightPuddle, PuddleVisuals.SolutionColor);
            });

            await pair.RunUntilSynced();

            await client.WaitAssertion(() =>
            {
                var left = client.EntMan.GetComponent<ClientPuddleComponent>(clientLeft);
                var right = client.EntMan.GetComponent<ClientPuddleComponent>(clientRight);

                Assert.That(right.SolutionColor, Is.EqualTo(Color.White));
                Assert.That(right.ShaderColor, Is.EqualTo(Color.White));
                Assert.That(left.EastShaderColor, Is.EqualTo(Color.White));
            });

            // Setting a new color should propagate to the puddle shader and adjacent blend color.
            await server.WaitPost(() =>
            {
                appearance.SetData(rightPuddle, PuddleVisuals.SolutionColor, blue);
            });

            await pair.RunUntilSynced();

            await client.WaitAssertion(() =>
            {
                var left = client.EntMan.GetComponent<ClientPuddleComponent>(clientLeft);
                var right = client.EntMan.GetComponent<ClientPuddleComponent>(clientRight);

                Assert.That(right.SolutionColor, Is.EqualTo(blue));
                Assert.That(right.ShaderColor, Is.EqualTo(blue));
                Assert.That(left.EastShaderColor, Is.EqualTo(blue));
            });

            // Deleting a puddle should make its former neighbor fall back to its own color.
            await server.WaitPost(() =>
            {
                server.EntMan.DeleteEntity(rightPuddle);
            });

            await pair.RunUntilSynced();

            await client.WaitAssertion(() =>
            {
                var left = client.EntMan.GetComponent<ClientPuddleComponent>(clientLeft);
                var leftColor = left.SolutionColor;

                Assert.That(left.ShaderColor, Is.EqualTo(leftColor));
                Assert.That(left.EastShaderColor, Is.EqualTo(leftColor));
            });
        }
    }
}
