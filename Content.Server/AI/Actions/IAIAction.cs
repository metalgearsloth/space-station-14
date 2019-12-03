using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Content.Server.AI.Actions
{

    public abstract class AiJob
    {
        private bool ForcedPriority { get; set; } = false;
        private int Priority => _priority;
        private int _priority;
        [CanBeNull] private AiJob _requiredJob;
        private ActionStatus Status => _status;
        private ActionStatus _status;
        public event Action StatusUpdate;
        private EventHandler statusisUpdated;

        public virtual void ChangePriority(int newPriority)
        {
            _priority = newPriority;
            ForcedPriority = true;
            StatusUpdate += () =>
            {
                if (Status == ActionStatus.Complete)
                {
                    ForcedPriority = false;
                }
            };
        }

        protected void ChangeStatus(ActionStatus status)
        {
            StatusUpdate?.Invoke();
            _status = status;
        }

        protected void OnStart()
        {

        }

        protected void OnFinish()
        {

        }

        public enum ActionStatus
        {
            CanExecute,
            Blocking,
            Complete,
        }
    }

    public class ActionList
    {
        public IEnumerable<AiJob> Actions = new List<AiJob>();
    }
}
