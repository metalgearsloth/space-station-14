using System;
using System.Collections.Generic;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.GameObjects.Components.AI
{
    public abstract class SharedAiDebugComponent : Component
    {
        public override string Name => "AIDebugger";
        public override uint? NetID => ContentNetIDs.AI_DEBUG;
    }


    [Serializable, NetSerializable]
    public class AiPlanMessage : ComponentMessage
    {
        public string RootTask { get; }

        public AiPlanMessage(string rootTask)
        {
            RootTask = rootTask;
        }
    }
}
