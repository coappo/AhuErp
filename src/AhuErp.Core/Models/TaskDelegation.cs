using System;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Делегирование поручения (Phase 11). Создаётся либо вручную через
    /// <c>DelegationService.Delegate</c>, либо автоматически — когда
    /// <c>TaskService.CreateTask</c> через <c>SubstitutionService</c>
    /// перенаправляет исполнителя на заместителя.
    /// </summary>
    public class TaskDelegation
    {
        public int Id { get; set; }

        public int TaskId { get; set; }
        public virtual DocumentTask Task { get; set; }

        public int FromEmployeeId { get; set; }
        public virtual Employee FromEmployee { get; set; }

        public int ToEmployeeId { get; set; }
        public virtual Employee ToEmployee { get; set; }

        public DateTime DelegatedAt { get; set; }

        [StringLength(512)]
        public string Comment { get; set; }
    }
}
