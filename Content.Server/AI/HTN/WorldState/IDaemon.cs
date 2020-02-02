namespace Content.Server.AI.HTN.WorldState
{
    /// <summary>
    /// Daemons are actively queried every n milliseconds; something like checking for nearby players is expensive so we do it occasionally
    /// </summary>
    public interface IDaemon
    {
        void Update(float frameTime);
    }
}
