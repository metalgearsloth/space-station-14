#nullable enable
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.GameObjects.EntitySystems;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Maths;

namespace Content.Client.GameObjects.EntitySystems
{
    /// <summary>
    ///     Provides a small ambient light player-side only for their attached entity
    /// </summary>
    [UsedImplicitly]
    internal sealed class LocalPlayerLightSystem : EntitySystem
    {
        private IEntity? _lightEntity;
        
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<PlayerAttachSysMessage>(HandlePlayerAttached);
        }

        private void HandlePlayerAttached(PlayerAttachSysMessage message)
        {
            if (_lightEntity?.HasComponent<PointLightComponent>() == true)
            {
                _lightEntity?.RemoveComponent<PointLightComponent>();
            }
            
            if (message.AttachedEntity == null || message.AttachedEntity.HasComponent<PointLightComponent>()) 
                return;
            
            _lightEntity = message.AttachedEntity;
            var pointLight = _lightEntity.AddComponent<PointLightComponent>();

            pointLight.Enabled = true;
            pointLight.Color = new Color(89, 90, 150);
            pointLight.Energy = 0.5f;
            pointLight.Radius = 2.0f;
        }
    }
}