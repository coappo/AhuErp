using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    public sealed class InMemoryDelegationRepository : IDelegationRepository
    {
        private readonly List<TaskDelegation> _items = new List<TaskDelegation>();
        private int _nextId = 1;

        public TaskDelegation Add(TaskDelegation delegation)
        {
            if (delegation == null) throw new ArgumentNullException(nameof(delegation));
            if (delegation.Id == 0) delegation.Id = _nextId++;
            else _nextId = Math.Max(_nextId, delegation.Id + 1);
            _items.Add(delegation);
            return delegation;
        }

        public IReadOnlyList<TaskDelegation> ListByTask(int taskId)
            => _items.Where(d => d.TaskId == taskId)
                     .OrderBy(d => d.DelegatedAt)
                     .ToList()
                     .AsReadOnly();
    }
}
