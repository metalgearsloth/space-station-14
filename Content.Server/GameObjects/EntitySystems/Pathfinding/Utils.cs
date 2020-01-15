namespace Content.Server.GameObjects.EntitySystems.Pathfinding
{
    public static class Utils
    {
        public static bool Traversable(int collisionMask, int nodeMask)
        {
            return (collisionMask & nodeMask) == 0;
        }
    }
}
