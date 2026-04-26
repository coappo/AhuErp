using System;
using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Phase 9 — фасад над <see cref="INotificationRepository"/> и
    /// <see cref="IEmailGateway"/>. Учитывает индивидуальные предпочтения
    /// сотрудника (<see cref="NotificationPreference"/>) и обрабатывает
    /// дедлайны через <see cref="TickReminders"/>.
    /// </summary>
    public interface INotificationService
    {
        Notification Create(int recipientId, NotificationKind kind, string title,
                            string body, int? docId = null, int? taskId = null,
                            int? approvalId = null);

        void MarkRead(int notificationId, int actorId);
        void MarkAllRead(int recipientId);

        IReadOnlyList<Notification> ListForUser(int recipientId, bool unreadOnly = false);
        int CountUnread(int recipientId);

        /// <summary>
        /// Идемпотентный обход всех активных задач: создаёт
        /// <see cref="NotificationKind.TaskDeadlineSoon"/> за 24 часа до Deadline
        /// и <see cref="NotificationKind.TaskOverdue"/> при наступлении просрочки.
        /// Повторный вызов в рамках того же события записей не дублирует.
        /// </summary>
        void TickReminders(DateTime now);
    }
}
