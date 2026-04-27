using System;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Phase 9 — in-app/e-mail уведомление сотрудника. Создаётся в момент
    /// событий (поручение назначено, согласование запрошено, дедлайн скоро и т.п.)
    /// и хранится до тех пор, пока сотрудник не отметит как прочитанное.
    /// </summary>
    public class Notification
    {
        public int Id { get; set; }

        public int RecipientId { get; set; }
        public virtual Employee Recipient { get; set; }

        public NotificationKind Kind { get; set; }

        [StringLength(512)]
        public string Title { get; set; }

        [StringLength(2048)]
        public string Body { get; set; }

        public int? RelatedDocumentId { get; set; }
        public int? RelatedTaskId { get; set; }
        public int? RelatedApprovalId { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? ReadAt { get; set; }

        /// <summary>EF6 не маппит вычисляемые свойства: <see cref="ReadAt"/> = null → не прочитано.</summary>
        public bool IsRead => ReadAt.HasValue;

        public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;

        /// <summary>Когда фактически отправлено по e-mail (если Channel = Email/Both).</summary>
        public DateTime? SentToEmailAt { get; set; }
    }
}
