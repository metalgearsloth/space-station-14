using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Content.Server.AI.Utility;
using Content.Server.AI.Utility.Actions;
using Content.Server.GameObjects.EntitySystems.AI;
using NUnit.Framework;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Reflection;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.NPCs
{
    [TestFixture]
    public class TestPrototypes : ContentIntegrationTest
    {
        [Test]
        public async Task BehaviorSetsTest()
        {
            var server = StartServerDummyTicker();
            await server.WaitIdleAsync();

            var protoManager = server.ResolveDependency<IPrototypeManager>();
            var reflectionManager = server.ResolveDependency<IReflectionManager>();

            server.Assert(() =>
            {
                foreach (var prototype in protoManager.EnumeratePrototypes<BehaviorSetPrototype>())
                {
                    foreach (var action in GetActions(prototype, protoManager))
                    {
                        var type = reflectionManager.LooseGetType(action);

                        Assert.That(typeof(IAiUtility).IsAssignableFrom(type));
                        Assert.That(type.GetConstructor(Type.EmptyTypes), Is.Not.EqualTo(null), $"No parameterless constructor for {action}");
                    }
                }
            });
        }

        private IEnumerable<string> GetActions(BehaviorSetPrototype prototype, IPrototypeManager prototypeManager)
        {
            if (prototype.Parent != null)
            {
                var parent = prototypeManager.Index<BehaviorSetPrototype>(prototype.Parent);
                foreach (var action in GetActions(parent, prototypeManager))
                {
                    yield return action;
                }
            }

            foreach (var action in prototype.Actions)
            {
                yield return action;
            }
        }
    }
}
