#nullable enable
using System.Collections.Generic;
using Content.Shared.GameObjects.EntitySystems;
using Robust.Server.Interfaces.Player;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.GameObjects.EntitySystems
{
    internal sealed class DecalSystem : SharedDecalSystem
    {
        [Dependency] private readonly IPlayerManager _playerManager = default!;

        private Dictionary<IPlayerSession, PlayerDecalOverlay> _knownPlayerChunks =
            new Dictionary<IPlayerSession, PlayerDecalOverlay>();

        private float _accumulatedTime = 0.5f;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<DecalMessage>(HandleDecalMessage);
            _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
        }

        private void HandleDecalMessage(DecalMessage message)
        {
            var gridId = message.Coordinates.GetGridId(EntityManager);

            if (gridId == GridId.Invalid)
                return;

            var indices = MapManager.GetGrid(gridId).GetTileRef(message.Coordinates).GridIndices;
            var chunk = GetOrCreateChunk(gridId, indices);

            var decal = new Decal(message.Coordinates, message.Color, message.RsiPath, message.State);
            var currentTick = GameTiming.CurTick;
            chunk.AddDecal(currentTick, decal);
            chunk.Dirty(currentTick);
        }

        public override void Shutdown()
        {
            base.Shutdown();
            _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
        }

        private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs eventArgs)
        {
            if (eventArgs.NewStatus != SessionStatus.InGame)
            {
                _knownPlayerChunks.Remove(eventArgs.Session);
                return;
            }

            if (!_knownPlayerChunks.ContainsKey(eventArgs.Session))
                _knownPlayerChunks[eventArgs.Session] = new PlayerDecalOverlay();
        }

        protected override void HandleGridRemoved(GridId gridId)
        {
            base.HandleGridRemoved(gridId);

            if (!Decals.TryGetValue(gridId, out var chunks))
                return;

            foreach (var (_, overlay) in _knownPlayerChunks)
            {
                foreach (var (_, chunk) in chunks)
                {
                    overlay.RemoveChunk(chunk);
                }
            }
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            var currentTick = GameTiming.CurTick;

            // If you were super pedantic you'd only update on:
            // - New connection
            // - Someone moving around in worldspace (given new grids can move into view)
            // - A new decal being made
            // buuuuttt this is easier for now, and we won't run it every tick coz who cares if a crayon shows up slightly late

            _accumulatedTime += frameTime;

            if (_accumulatedTime <= 0.2f)
                return;

            _accumulatedTime -= 0.2f;

            foreach (var player in _playerManager.GetAllPlayers())
            {
                if (player.AttachedEntity == null)
                    continue;

                var data = _knownPlayerChunks[player];
                var chunksInRange = GetChunksInRange(player.AttachedEntity);
                var decalsToSend = new Dictionary<GridId, List<Decal>>();

                // Update each chunk for client and determine which decals need sending.
                // If only 1 is updated for a particular chunk then we'll only send that 1 decal.
                foreach (var chunk in chunksInRange)
                {
                    data.UpdateChunk(currentTick, chunk);
                    var lastSeen = data.LastSeen(chunk);

                    // Don't judge the pyramid
                    if (lastSeen >= currentTick)
                    {
                        foreach (var (decal, lastTick) in chunk.Decals)
                        {
                            if (lastTick <= lastSeen)
                            {
                                if (!decalsToSend.TryGetValue(chunk.GridId, out var existingDecals))
                                {
                                    existingDecals = new List<Decal>();
                                    decalsToSend[chunk.GridId] = existingDecals;
                                }

                                existingDecals.Add(decal);
                            }
                        }
                    }
                }

                if (decalsToSend.Count == 0)
                    continue;

                RaiseNetworkEvent(new DecalOverlayMessage(decalsToSend), player.ConnectedClient);
            }
        }
    }

    internal sealed class PlayerDecalOverlay
    {
        private readonly Dictionary<DecalChunk, GameTick> _lastSent =
            new Dictionary<DecalChunk, GameTick>();

        public GameTick LastSeen(DecalChunk chunk)
        {
            return _lastSent[chunk];
        }

        public void UpdateChunk(GameTick currentTick, DecalChunk chunk)
        {
            if (_lastSent.TryGetValue(chunk, out var last) && last >= chunk.LastModifiedTick)
                return;

            _lastSent[chunk] = currentTick;

            return;
        }

        public void RemoveChunk(DecalChunk chunk)
        {
            _lastSent.Remove(chunk);
        }
    }
}
