using System;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Phase 10 — сохранённый поиск. <see cref="FilterJson"/> хранит
    /// сериализованный <see cref="Services.DocumentSearchFilter"/>;
    /// если <see cref="IsShared"/>=true, поиск виден всем сотрудникам.
    /// </summary>
    public class SavedSearch
    {
        public int Id { get; set; }
        public int OwnerId { get; set; }
        public virtual Employee Owner { get; set; }

        [Required, StringLength(128)]
        public string Name { get; set; }

        public string FilterJson { get; set; }

        public bool IsShared { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
