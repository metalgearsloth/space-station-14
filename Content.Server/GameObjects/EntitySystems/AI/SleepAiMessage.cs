using Content.Server.GameObjects.Components.Movement;
using Robust.Server.AI;
using Robust.Shared.GameObjects;

namespace Content.Server.GameObjects.EntitySystems.AI
{
    /// <summary>
    ///     Indicates whether an AI should be updated by the AiSystem or not.
    ///     Useful to sleep AI when they die or otherwise should be inactive.
    /// </summary>
    internal sealed class SleepAiMessage : EntitySystemMessage
    {
        /// <summary>
        ///     Sleep or awake.
        /// </summary>
        public bool Sleep { get; }
        public NPCComponent Component { get; }

        public SleepAiMessage(NPCComponent component, bool sleep)
        {
            Component = component;
            Sleep = sleep;
        }
    }
}
