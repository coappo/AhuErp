using System;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Замещение должности (Phase 11). На период [<see cref="From"/>;<see cref="To"/>]
    /// все входящие задачи и/или согласования сотрудника
    /// <see cref="OriginalEmployeeId"/> автоматически перенаправляются
    /// заместителю <see cref="SubstituteEmployeeId"/> в зависимости от <see cref="Scope"/>.
    /// </summary>
    public class Substitution
    {
        public int Id { get; set; }

        public int OriginalEmployeeId { get; set; }
        public virtual Employee OriginalEmployee { get; set; }

        public int SubstituteEmployeeId { get; set; }
        public virtual Employee SubstituteEmployee { get; set; }

        public DateTime From { get; set; }
        public DateTime To { get; set; }

        public SubstitutionScope Scope { get; set; }

        [StringLength(512)]
        public string Reason { get; set; }

        /// <summary>
        /// Активная запись попадает в <c>ResolveActualExecutor</c>. Признак
        /// сбрасывается командой <c>SubstitutionService.Cancel</c>.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>Сотрудник, оформивший замещение (для аудита/отчётов).</summary>
        public int CreatedById { get; set; }

        /// <summary>Замещение охватывает заданный момент времени.</summary>
        public bool CoversMoment(DateTime now)
        {
            return IsActive && From <= now && now <= To;
        }
    }
}
