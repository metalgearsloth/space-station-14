using System.Linq;
using Content.Shared.Construction;
using Robust.Server.Containers;
using Robust.Shared.Containers;
using Robust.Shared.Utility;

namespace Content.Server.Construction.Completions
{
    [DataDefinition]
    public sealed partial class DeleteEntitiesInContainer : IGraphAction
    {
        [DataField("container")] public string Container { get; private set; } = string.Empty;

        public void PerformAction(EntityUid uid, EntityUid? userUid, IEntityManager entityManager)
        {
            if (string.IsNullOrEmpty(Container))
                return;
            var containerSys = entityManager.EntitySysManager.GetEntitySystem<ContainerSystem>();

            if (!containerSys.TryGetContainer(uid, Container, out var container))
                return;

            foreach (var contained in container.ContainedEntities.ToValueList())
            {
                if(containerSys.Remove(contained, container))
                    entityManager.QueueDeleteEntity(contained);
            }
        }
    }
}
