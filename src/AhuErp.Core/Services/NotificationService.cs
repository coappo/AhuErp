using System;
using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Реализация <see cref="INotificationService"/>. Учитывает
    /// <see cref="NotificationPreference"/> для каждого получателя:
    /// если <see cref="NotificationPreference.IsEnabled"/> = false — запись не
    /// создаётся; если канал = Email — in-app не пишется, только e-mail;
    /// если канал = Both — in-app + e-mail.
    /// </summary>
    public sealed class NotificationService : INotificationService
    {
        private readonly INotificationRepository _repo;
        private readonly IEmployeeRepository _employees;
        private readonly ITaskRepository _tasks;
        private readonly IAuditService _audit;
        private readonly IEmailGateway _email;

        public NotificationService(
            INotificationRepository repo,
            IEmployeeRepository employees,
            ITaskRepository tasks,
            IAuditService audit,
            IEmailGateway email = null)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _employees = employees ?? throw new ArgumentNullException(nameof(employees));
            _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _email = email;
        }

        public Notification Create(int recipientId, NotificationKind kind, string title,
                                   string body, int? docId = null, int? taskId = null,
                                   int? approvalId = null)
        {
            if (recipientId <= 0) throw new ArgumentException("Получатель обязателен.", nameof(recipientId));
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Заголовок обязателен.", nameof(title));

            var pref = _repo.GetPreference(recipientId, kind);
            if (pref != null && !pref.IsEnabled)
            {
                // Сотрудник явно отписан от этого вида уведомлений.
                return null;
            }

            var channel = pref?.Channel ?? NotificationChannel.InApp;
            Notification stored = null;

            // In-app пишем при InApp/Both.
            if (channel == NotificationChannel.InApp || channel == NotificationChannel.Both)
            {
                stored = _repo.Add(new Notification
                {
                    RecipientId = recipientId,
                    Kind = kind,
                    Title = title,
                    Body = body,
                    RelatedDocumentId = docId,
                    RelatedTaskId = taskId,
                    RelatedApprovalId = approvalId,
                    CreatedAt = DateTime.Now,
                    Channel = channel,
                });
                _audit.Record(AuditActionType.NotificationSent, nameof(Notification),
                    stored.Id, recipientId,
                    newValues: $"Kind={kind}; Channel={channel}");
            }

            // E-mail отправляем при Email/Both, если есть адрес и шлюз.
            if ((channel == NotificationChannel.Email || channel == NotificationChannel.Both)
                && _email != null)
            {
                var addr = !string.IsNullOrWhiteSpace(pref?.EmailOverride)
                    ? pref.EmailOverride
                    : _employees.GetById(recipientId)?.Email;
                if (!string.IsNullOrWhiteSpace(addr))
                {
                    try
                    {
                        _email.Send(addr, title, body);
                        if (stored != null)
                        {
                            stored.SentToEmailAt = DateTime.Now;
                            _repo.Update(stored);
                        }
                    }
                    catch
                    {
                        // SMTP сбой не должен валить бизнес-операцию.
                    }
                }
            }

            return stored;
        }

        public void MarkRead(int notificationId, int actorId)
        {
            var n = _repo.Get(notificationId);
            if (n == null || n.ReadAt.HasValue) return;
            n.ReadAt = DateTime.Now;
            _repo.Update(n);
        }

        public void MarkAllRead(int recipientId)
        {
            foreach (var n in _repo.ListByRecipient(recipientId, unreadOnly: true))
            {
                n.ReadAt = DateTime.Now;
                _repo.Update(n);
            }
        }

        public IReadOnlyList<Notification> ListForUser(int recipientId, bool unreadOnly = false)
            => _repo.ListByRecipient(recipientId, unreadOnly);

        public int CountUnread(int recipientId) => _repo.CountUnread(recipientId);

        public void TickReminders(DateTime now)
        {
            // Просто проходим по всем активным задачам. Кол-во записей в
            // ERP-инсталляции МКУ ожидается на порядок 10^3, что укладывается
            // в одиночный проход.
            foreach (var t in _tasks.ListAll())
            {
                if (t.Status == DocumentTaskStatus.Completed
                    || t.Status == DocumentTaskStatus.Cancelled) continue;

                // 24 часа до дедлайна → DeadlineSoon (один раз).
                if (now < t.Deadline && (t.Deadline - now).TotalHours <= 24
                    && _repo.ListByRelatedTask(t.Id, NotificationKind.TaskDeadlineSoon).Count == 0)
                {
                    Create(t.ExecutorId, NotificationKind.TaskDeadlineSoon,
                        $"Срок поручения скоро истекает (#{t.Id})",
                        BuildBody(t),
                        docId: t.DocumentId, taskId: t.Id);
                }

                // Просрочено → Overdue (один раз).
                if (now > t.Deadline
                    && _repo.ListByRelatedTask(t.Id, NotificationKind.TaskOverdue).Count == 0)
                {
                    Create(t.ExecutorId, NotificationKind.TaskOverdue,
                        $"Поручение просрочено (#{t.Id})",
                        BuildBody(t),
                        docId: t.DocumentId, taskId: t.Id);
                }
            }
        }

        private static string BuildBody(DocumentTask t)
            => $"Документ #{t.DocumentId}. Срок: {t.Deadline:dd.MM.yyyy HH:mm}. {t.Description}";
    }
}
