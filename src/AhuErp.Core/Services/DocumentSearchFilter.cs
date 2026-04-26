using System;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Фильтр для поиска и листинга документов в журналах регистрации
    /// и глобальном поиске. Все поля опциональны: <c>null</c> означает «не
    /// ограничивать по этому критерию».
    /// </summary>
    public sealed class DocumentSearchFilter
    {
        /// <summary>Полнотекстовый запрос (LIKE по Title, Summary, RegistrationNumber, Correspondent, IncomingNumber).</summary>
        public string Text { get; set; }

        /// <summary>Направление документа (входящий/исходящий/внутренний).</summary>
        public DocumentDirection? Direction { get; set; }

        /// <summary>Один из статусов; <see cref="StatusIn"/> — несколько.</summary>
        public DocumentStatus? Status { get; set; }

        /// <summary>Множественный фильтр по статусам (приоритетнее <see cref="Status"/>).</summary>
        public DocumentStatus[] StatusIn { get; set; }

        /// <summary>Период по дате регистрации (или CreationDate, если не зарегистрирован).</summary>
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }

        /// <summary>Корреспондент (LIKE).</summary>
        public string Correspondent { get; set; }

        /// <summary>Идентификатор номенклатурного дела.</summary>
        public int? NomenclatureCaseId { get; set; }

        /// <summary>Справочный вид документа.</summary>
        public int? DocumentTypeRefId { get; set; }

        /// <summary>Ответственный/исполнитель.</summary>
        public int? AssignedEmployeeId { get; set; }

        /// <summary>Только просроченные (по <see cref="Document.Deadline"/>).</summary>
        public bool OverdueOnly { get; set; }

        /// <summary>Только зарегистрированные (с непустым <see cref="Document.RegistrationNumber"/>).</summary>
        public bool RegisteredOnly { get; set; }
    }
}
