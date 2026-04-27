using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Сервис делегирования поручений (Phase 11). Поддерживает явное
    /// «передать поручение» (DelegationService.Delegate) и историю передач.
    /// </summary>
    public interface IDelegationService
    {
        /// <summary>
        /// Передать поручение другому сотруднику. Поменять <c>ExecutorId</c>
        /// у <c>DocumentTask</c>, добавить запись <see cref="TaskDelegation"/>,
        /// записать аудит <c>TaskDelegated</c>.
        /// </summary>
        TaskDelegation Delegate(int taskId, int toEmployeeId, int actorId, string comment);

        /// <summary>История делегирований по поручению (старшие записи — раньше).</summary>
        IReadOnlyList<TaskDelegation> History(int taskId);
    }
}
