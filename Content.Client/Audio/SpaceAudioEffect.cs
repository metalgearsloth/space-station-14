using Content.Shared.Maps;
using JetBrains.Annotations;
using Robust.Client.Audio;
using Robust.Client.Interfaces.Graphics;
using Robust.Shared.Audio;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Map;

namespace Content.Client.Audio
{
    [UsedImplicitly]
    public sealed class SpaceAudioEffect : IAudioEffect
    {
        public bool TrySetEntityEffect(IClydeAudioSource source, IEntity entity)
        {
            var tile = entity.Transform.Coordinates.GetTileRef();
            if (tile != null && !tile.Value.Tile.IsEmpty) return false;
            source.SetAudioEffect(AudioEffect.Space);
            return true;
        }

        public bool TrySetCoordsEffect(IClydeAudioSource source, EntityCoordinates coordinates)
        {
            var tile = coordinates.GetTileRef();
            if (tile != null && !tile.Value.Tile.IsEmpty) return false;
            source.SetAudioEffect(AudioEffect.Space);
            return true;
        }
    }
}
