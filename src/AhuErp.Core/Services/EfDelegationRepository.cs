using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    public sealed class EfDelegationRepository : IDelegationRepository
    {
        private readonly AhuDbContext _ctx;

        public EfDelegationRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public TaskDelegation Add(TaskDelegation delegation)
        {
            _ctx.TaskDelegations.Add(delegation);
            _ctx.SaveChanges();
            return delegation;
        }

        public IReadOnlyList<TaskDelegation> ListByTask(int taskId)
            => _ctx.TaskDelegations
                .Include(d => d.FromEmployee)
                .Include(d => d.ToEmployee)
                .Where(d => d.TaskId == taskId)
                .OrderBy(d => d.DelegatedAt)
                .ToList()
                .AsReadOnly();
    }
}
