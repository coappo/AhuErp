using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>Репозиторий <see cref="Notification"/> и <see cref="NotificationPreference"/>.</summary>
    public interface INotificationRepository
    {
        Notification Add(Notification n);
        Notification Get(int id);
        void Update(Notification n);

        IReadOnlyList<Notification> ListByRecipient(int recipientId, bool unreadOnly);
        int CountUnread(int recipientId);

        /// <summary>Все уведомления выбранного типа, связанные с задачей (для общей дедупликации).</summary>
        IReadOnlyList<Notification> ListByRelatedTask(int taskId, NotificationKind kind);

        /// <summary>
        /// Уведомления выбранного типа, связанные с задачей, отправленные конкретному
        /// получателю. Используется в <see cref="INotificationService.TickReminders"/>
        /// для дедупликации с учётом фактического исполнителя — после делегирования
        /// задачи на нового сотрудника общая (per-task) проверка ложно-срабатывает,
        /// потому что у задачи уже есть напоминание для прежнего исполнителя.
        /// </summary>
        IReadOnlyList<Notification> ListByRelatedTaskAndRecipient(
            int taskId, NotificationKind kind, int recipientId);

        // Preferences ------------------------------------------------------

        NotificationPreference GetPreference(int employeeId, NotificationKind kind);
        void SetPreference(NotificationPreference pref);
        IReadOnlyList<NotificationPreference> ListPreferences(int employeeId);
    }
}
