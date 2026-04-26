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

        /// <summary>Все уведомления выбранного типа, связанные с задачей (для дедупликации).</summary>
        IReadOnlyList<Notification> ListByRelatedTask(int taskId, NotificationKind kind);

        // Preferences ------------------------------------------------------

        NotificationPreference GetPreference(int employeeId, NotificationKind kind);
        void SetPreference(NotificationPreference pref);
        IReadOnlyList<NotificationPreference> ListPreferences(int employeeId);
    }
}
