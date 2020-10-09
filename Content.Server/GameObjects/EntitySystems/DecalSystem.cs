#nullable enable
using System.Collections.Generic;
using Content.Shared.GameObjects.EntitySystems;
using Robust.Server.Interfaces.Player;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.GameObjects.EntitySystems
{
    internal sealed class DecalSystem : SharedDecalSystem
    {
        [Dependency] private readonly IMapManager _mapManager = default!;
        [Dependency] private readonly IPlayerManager _playerManager = default!;
        
        private Dictionary<IPlayerSession, PlayerDecalOverlay> _knownPlayerChunks = 
            new Dictionary<IPlayerSession, PlayerDecalOverlay>();

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<DecalMessage>(HandleDecalMessage);
            // TODO: Handle player sessions and shit
            _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
            _mapManager.OnGridRemoved += OnGridRemoved;
        }

        public override void Shutdown()
        {
            base.Shutdown();
            _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
            _mapManager.OnGridRemoved -= OnGridRemoved;
        }

        private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs eventArgs)
        {
            if (eventArgs.NewStatus != SessionStatus.InGame)
            {
                if (_knownPlayerChunks.ContainsKey(eventArgs.Session))
                    _knownPlayerChunks.Remove(eventArgs.Session);

                return;
            }

            if (!_knownPlayerChunks.ContainsKey(eventArgs.Session))
                _knownPlayerChunks[eventArgs.Session] = new PlayerDecalOverlay();
        }

        private void OnGridRemoved(GridId gridId)
        {
            
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            foreach (var player in _playerManager.GetAllPlayers())
            {
                if (player.AttachedEntity == null)
                    continue;

                var data = _knownPlayerChunks[player];
                var decalsAdded = new List<Decal>();

                // TODO: Foreach chunk in range add to overlay if not already
                // Then, foreach chunk in range check its dirty and if its < what the player knows then ship it
                // Also update at the same time
                
                /*
                 * TODO
                 * Get Chunks in range
                 * Compile dirty decals from each chunk and bundle it
                 * Send 1 message to client
                 */

                if (decalsAdded.Count == 0)
                    continue;
                
                RaiseNetworkEvent(new DecalOverlayMessage(), player.ConnectedClient);
            }
        }
    }

    internal sealed class PlayerDecalOverlay
    {
        private readonly Dictionary<DecalChunk, GameTick> _lastSent =
            new Dictionary<DecalChunk, GameTick>();

        public void Reset()
        {
            _lastSent.Clear();
        }

        public GameTick? LastSeen(DecalChunk chunk)
        {
            if (!_lastSent.TryGetValue(chunk, out var lastSeen))
                return null;

            return lastSeen;
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