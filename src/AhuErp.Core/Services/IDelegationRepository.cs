using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>Репозиторий делегирований поручений (Phase 11).</summary>
    public interface IDelegationRepository
    {
        TaskDelegation Add(TaskDelegation delegation);
        IReadOnlyList<TaskDelegation> ListByTask(int taskId);
    }
}
