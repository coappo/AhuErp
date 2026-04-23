using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Базовая сущность документа.
    /// Наследники (например, <see cref="ArchiveRequest"/>) различаются EF6 TPH-дискриминатором.
    /// </summary>
    public class Document
    {
        public int Id { get; set; }

        public DocumentType Type { get; set; }

        [Required]
        [StringLength(512)]
        public string Title { get; set; }

        public DateTime CreationDate { get; set; }

        public DateTime Deadline { get; set; }

        public DocumentStatus Status { get; set; }

        public int? AssignedEmployeeId { get; set; }

        public virtual Employee AssignedEmployee { get; set; }

        public virtual ICollection<VehicleTrip> VehicleTrips { get; set; } = new HashSet<VehicleTrip>();

        /// <summary>
        /// Документ просрочен, если срок истёк, а работа не завершена/не отменена.
        /// </summary>
        public bool IsOverdue(DateTime now)
        {
            return Deadline < now
                   && Status != DocumentStatus.Completed
                   && Status != DocumentStatus.Cancelled;
        }
    }
}
