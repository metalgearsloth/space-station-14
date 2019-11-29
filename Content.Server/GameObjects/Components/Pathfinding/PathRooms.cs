using System.Collections.Generic;
using Robust.Shared.Map;

namespace Content.Server.GameObjects.Components.Pathfinding
{
    // TODO: Add to IoC resolver
    public interface IPathRooms
    {
        IEnumerable<IPathRoom> Rooms { get; }
    }

    public interface IPathRoom
    {
        IEnumerable<TileRef> Airlocks();
        IEnumerable<TileRef> Tiles();
        IEnumerable<TileRef> Walls();
    }

    public class PathRooms : IPathRooms
    {
        // TODO: Floodfill every single tile and get rooms
        public IEnumerable<IPathRoom> Rooms => _rooms;
        private IEnumerable<IPathRoom> _rooms = new List<IPathRoom>();

        void Refresh()
        {
            // Asynchronously refresh each room
        }

        void MergeRooms(IPathRoom room1, IPathRoom room2)
        {
            // These should collapse inwards
        }

        void RefreshRoom(IPathRoom room)
        {
            // Will rebuild the room
        }

        void GetRooms()
        {
            // Floodfill this bitch
        }
    }
}
