#nullable enable
using System.Collections.Generic;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using YamlDotNet.RepresentationModel;

namespace Content.Server.AI.Utility
{
    /// <summary>
    ///     Encompasses a group of utility actions into a set.
    ///     Includes regular and expandable actions.
    /// </summary>
    [Prototype("behaviorSet")]
    public sealed class BehaviorSetPrototype : IIndexedPrototype, IPrototype
    {
        public string ID { get; private set; } = default!;

        /// <summary>
        ///     BehaviorSet to inherit from
        /// </summary>
        public string? Parent { get; private set; } = default!;

        public IReadOnlyCollection<string> Actions => _actions;
        private List<string> _actions = default!;

        public void LoadFrom(YamlMappingNode mapping)
        {
            var serializer = YamlObjectSerializer.NewReader(mapping);

            serializer.DataField(this, x => x.ID, "id", string.Empty);
            serializer.DataField(this, x => x.Parent, "parent", null);
            serializer.DataReadWriteFunction(
                "actions",
                new List<string>(),
                value => _actions = value,
                () => _actions);
        }
    }
}
