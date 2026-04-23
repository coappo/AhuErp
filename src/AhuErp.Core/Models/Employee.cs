using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Сотрудник учреждения. Может быть назначен ответственным за документ.
    /// </summary>
    public class Employee
    {
        public int Id { get; set; }

        [Required]
        [StringLength(256)]
        public string FullName { get; set; }

        [StringLength(256)]
        public string Position { get; set; }

        public virtual ICollection<Document> AssignedDocuments { get; set; } = new HashSet<Document>();
    }
}
