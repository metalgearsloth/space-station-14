#nullable enable
using System;
using Content.Shared.Interfaces;
using Content.Shared.Interfaces.GameObjects.Components;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.Interfaces.Random;
using Robust.Shared.Interfaces.Timing;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Timers;
using Robust.Shared.Utility;

namespace Content.Server.GameObjects.Components.Movement
{
    /// <summary>
    ///     Randomly teleport the user.
    /// </summary>
    [RegisterComponent]
    public class RandomHandTeleporterComponent : Component, IAfterInteract, IUse
    {
        public override string Name => "RandomHandTeleporter";

        private AppearanceComponent? _appearanceComponent;
        
        private TimeSpan _lastUse = TimeSpan.Zero;
        private float _cooldown;
        private string _portalPrototype;

        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);
            serializer.DataField(ref _cooldown, "cooldown", 5.0f);
            DebugTools.Assert(_cooldown >= 0.0f);
            serializer.DataField(ref _portalPrototype, "portalPrototype", "Portal");
            DebugTools.Assert(!string.IsNullOrEmpty(_portalPrototype));
        }

        public override void Initialize()
        {
            base.Initialize();
            Owner.TryGetComponent(out _appearanceComponent);
        }

        void IAfterInteract.AfterInteract(AfterInteractEventArgs eventArgs)
        {
            TryRandomPortal(eventArgs.User);
        }

        bool IUse.UseEntity(UseEntityEventArgs eventArgs)
        {
            return TryRandomPortal(eventArgs.User);
        }

        private bool TryRandomPortal(IEntity user)
        {
            var currentTime = IoCManager.Resolve<IGameTiming>().CurTime;
            if ((currentTime - _lastUse).TotalSeconds < _cooldown)
            {
                return false;
            }

            _lastUse = currentTime;
            // We'll trigger cd just so we don't spam this check.
            if (!PortalComponent.CanSpawnOn(user))
            {
                user.PopupMessage(Owner, Loc.GetString("Can't spawn a portal here!"));
                return false;
            }
            
            RandomPortal();

            if (_appearanceComponent != null)
            {
                //_appearanceComponent?.SetData();
                Timer.Spawn(TimeSpan.FromSeconds(_cooldown), () =>
                {
                    if (!Owner.Deleted && _appearanceComponent != null && !_appearanceComponent.Deleted)
                    {
                        //_appearanceComponent.SetData();
                    }
                });
            }

            return true;
        }

        private GridCoordinates FindRandomGridCoordinates(GridId gridId)
        {
            var robustRandom = IoCManager.Resolve<IRobustRandom>();
            var mapManager = IoCManager.Resolve<IMapManager>();
            var gridBounds = mapManager.GetGrid(gridId).WorldBounds;
            var randomIndices = new MapIndices(robustRandom.Next((int) gridBounds.Left, (int) gridBounds.Right), robustRandom.Next((int) gridBounds.Bottom, (int) gridBounds.Top));
            // TODO: VERIFY
            return randomIndices.ToGridCoordinates(mapManager, gridId);
        }

        private void RandomPortal()
        {
            var entityManager = IoCManager.Resolve<IEntityManager>();
            var currentPortalEntity = entityManager.SpawnEntity(_portalPrototype, Owner.Transform.MapPosition);
            var currentPortal = currentPortalEntity.GetComponent<PortalComponent>();
            var targetPortalEntity = entityManager.SpawnEntity(_portalPrototype, FindRandomGridCoordinates(Owner.Transform.GridID));
            var targetPortal = targetPortalEntity.GetComponent<PortalComponent>();
            currentPortal.ConnectTo(targetPortalEntity);
            targetPortal.ConnectTo(currentPortalEntity);

            currentPortal.PortalIntersecting();
            targetPortal.PortalIntersecting();
        }
    }
}