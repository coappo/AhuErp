using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Phase 9 — индивидуальные настройки уведомлений сотрудника.
    /// Один сотрудник может иметь по одной записи на каждый
    /// <see cref="NotificationKind"/>. Если записи нет — действуют умолчания
    /// (Channel = InApp, IsEnabled = true).
    /// </summary>
    public class NotificationPreference
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        public virtual Employee Employee { get; set; }

        public NotificationKind Kind { get; set; }

        public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;

        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Альтернативный e-mail-адрес для именно этого вида уведомлений.
        /// Если пусто, используется <see cref="Employee.Email"/>.
        /// </summary>
        [StringLength(256)]
        public string EmailOverride { get; set; }
    }
}
