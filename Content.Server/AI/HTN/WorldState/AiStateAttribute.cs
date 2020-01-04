using System;

namespace Content.Server.AI.HTN.WorldState
{
    /// <summary>
    ///
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class AiStateAttribute : Attribute
    {
    }

    /// <summary>
    ///
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class AiEnumerableStateAttribute : Attribute
    {
    }
}
