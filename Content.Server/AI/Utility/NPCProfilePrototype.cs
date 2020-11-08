#nullable enable
using System.Collections.Generic;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using YamlDotNet.RepresentationModel;

namespace Content.Server.AI.Utility
{
    /// <summary>
    ///     Encompasses a group of <see cref="BehaviorSetPrototype"/> that make up an NPC.
    /// </summary>
    [Prototype("NPCProfile")]
    public sealed class NPCProfilePrototype : IIndexedPrototype, IPrototype
    {
        public string ID { get; private set; } = default!;

        /// <summary>
        ///     Profile to inherit from
        /// </summary>
        public string? Parent { get; private set; } = default!;

        public IReadOnlyCollection<string> BehaviorSets => _behaviorSets;
        private List<string> _behaviorSets = default!;

        public void LoadFrom(YamlMappingNode mapping)
        {
            var serializer = YamlObjectSerializer.NewReader(mapping);

            serializer.DataField(this, x => x.ID, "id", string.Empty);
            serializer.DataField(this, x => x.Parent, "parent", null);
            serializer.DataReadWriteFunction(
                "behaviorSets",
                new List<string>(),
                value => _behaviorSets = value,
                () => _behaviorSets);
        }
    }
}
