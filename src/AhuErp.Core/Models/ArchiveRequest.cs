using System;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Архивная справка. Регламентный срок исполнения — 30 дней с момента регистрации.
    /// Закрывается только при наличии скан-копий паспорта и трудовой книжки.
    /// </summary>
    public class ArchiveRequest : Document
    {
        /// <summary>
        /// Регламентный срок обработки архивного запроса (в календарных днях).
        /// </summary>
        public const int DefaultDeadlineDays = 30;

        public bool HasPassportScan { get; set; }

        public bool HasWorkBookScan { get; set; }

        public ArchiveRequest()
        {
            Type = DocumentType.Archive;
        }

        /// <summary>
        /// Устанавливает регламентный срок исполнения: <paramref name="creationDate"/> + 30 дней.
        /// </summary>
        public void InitializeDeadline(DateTime creationDate)
        {
            CreationDate = creationDate;
            Deadline = creationDate.AddDays(DefaultDeadlineDays);
        }

        /// <summary>
        /// Запрос может быть закрыт только при полном комплекте скан-копий.
        /// </summary>
        public bool CanCompleteRequest()
        {
            return HasPassportScan && HasWorkBookScan;
        }
    }
}
