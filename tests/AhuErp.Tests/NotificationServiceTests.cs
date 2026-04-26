using System;
using System.Linq;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Phase 9 — поведение <see cref="NotificationService"/>:
    /// учёт <see cref="NotificationPreference"/>, e-mail-канал, MarkRead/CountUnread,
    /// идемпотентный <see cref="NotificationService.TickReminders"/>.
    /// </summary>
    public class NotificationServiceTests
    {
        private readonly InMemoryNotificationRepository _repo = new InMemoryNotificationRepository();
        private readonly InMemoryAuditLogRepository _auditRepo = new InMemoryAuditLogRepository();
        private readonly InMemoryEmployeeRepository _employees;
        private readonly InMemoryTaskRepository _tasks = new InMemoryTaskRepository();
        private readonly InMemoryDocumentRepository _docs = new InMemoryDocumentRepository();
        private readonly NoOpEmailGateway _email = new NoOpEmailGateway();
        private readonly AuditService _audit;
        private readonly NotificationService _service;

        public NotificationServiceTests()
        {
            _employees = new InMemoryEmployeeRepository(new[]
            {
                new Employee { Id = 1, FullName = "Иванов И.И.", Email = "ivanov@bmr", Role = EmployeeRole.Admin },
                new Employee { Id = 2, FullName = "Петров П.П.", Email = "petrov@bmr", Role = EmployeeRole.Manager },
            });
            _audit = new AuditService(_auditRepo);
            _service = new NotificationService(_repo, _employees, _tasks, _audit, _email);
        }

        [Fact]
        public void Create_persists_inapp_notification_and_writes_audit()
        {
            var n = _service.Create(2, NotificationKind.TaskAssigned, "Заголовок", "Тело",
                docId: 7, taskId: 8);

            Assert.NotNull(n);
            Assert.Equal(NotificationChannel.InApp, n.Channel);
            Assert.Single(_repo.ListByRecipient(2, unreadOnly: false));
            Assert.Equal(1, _audit.Query(new AuditQueryFilter { ActionType = AuditActionType.NotificationSent }).Count);
        }

        [Fact]
        public void Create_skips_when_preference_disabled()
        {
            _repo.SetPreference(new NotificationPreference
            {
                EmployeeId = 2,
                Kind = NotificationKind.TaskAssigned,
                Channel = NotificationChannel.InApp,
                IsEnabled = false,
            });

            var n = _service.Create(2, NotificationKind.TaskAssigned, "x", "y");
            Assert.Null(n);
            Assert.Empty(_repo.ListByRecipient(2, unreadOnly: false));
        }

        [Fact]
        public void Create_email_channel_does_not_persist_inapp_but_sends_mail()
        {
            _repo.SetPreference(new NotificationPreference
            {
                EmployeeId = 2,
                Kind = NotificationKind.TaskAssigned,
                Channel = NotificationChannel.Email,
                IsEnabled = true,
            });

            var n = _service.Create(2, NotificationKind.TaskAssigned, "Тема", "Тело");
            Assert.Null(n); // in-app не пишем
            Assert.Empty(_repo.ListByRecipient(2, unreadOnly: false));
            Assert.Single(_email.Sent);
            Assert.Equal("petrov@bmr", _email.Sent[0].To);
        }

        [Fact]
        public void Create_inapp_channel_does_not_send_mail()
        {
            _service.Create(2, NotificationKind.TaskAssigned, "x", "y");
            Assert.Empty(_email.Sent);
        }

        [Fact]
        public void Create_both_channel_sends_mail_and_persists()
        {
            _repo.SetPreference(new NotificationPreference
            {
                EmployeeId = 2,
                Kind = NotificationKind.TaskAssigned,
                Channel = NotificationChannel.Both,
                IsEnabled = true,
            });

            var n = _service.Create(2, NotificationKind.TaskAssigned, "x", "y");
            Assert.NotNull(n);
            Assert.NotNull(n.SentToEmailAt);
            Assert.Single(_email.Sent);
        }

        [Fact]
        public void Create_email_override_used_when_set()
        {
            _repo.SetPreference(new NotificationPreference
            {
                EmployeeId = 2,
                Kind = NotificationKind.TaskAssigned,
                Channel = NotificationChannel.Both,
                IsEnabled = true,
                EmailOverride = "alt@bmr",
            });

            _service.Create(2, NotificationKind.TaskAssigned, "x", "y");
            Assert.Equal("alt@bmr", _email.Sent[0].To);
        }

        [Fact]
        public void CountUnread_changes_after_MarkRead()
        {
            _service.Create(1, NotificationKind.System, "a", "b");
            _service.Create(1, NotificationKind.System, "c", "d");
            Assert.Equal(2, _service.CountUnread(1));

            var first = _repo.ListByRecipient(1, unreadOnly: false).First();
            _service.MarkRead(first.Id, actorId: 1);
            Assert.Equal(1, _service.CountUnread(1));
        }

        [Fact]
        public void MarkAllRead_zeroes_unread()
        {
            _service.Create(1, NotificationKind.System, "a", "b");
            _service.Create(1, NotificationKind.System, "c", "d");
            _service.MarkAllRead(1);
            Assert.Equal(0, _service.CountUnread(1));
        }

        [Fact]
        public void TickReminders_creates_DeadlineSoon_once()
        {
            var task = _tasks.AddTask(new DocumentTask
            {
                DocumentId = 1,
                AuthorId = 1,
                ExecutorId = 2,
                Description = "Подготовить",
                Deadline = DateTime.Now.AddHours(12),
                Status = DocumentTaskStatus.New,
                CreatedAt = DateTime.Now.AddHours(-1),
            });

            _service.TickReminders(DateTime.Now);
            _service.TickReminders(DateTime.Now); // повторный вызов не должен дублировать
            var list = _repo.ListByRelatedTask(task.Id, NotificationKind.TaskDeadlineSoon);
            Assert.Single(list);
        }

        [Fact]
        public void TickReminders_creates_Overdue_once()
        {
            var task = _tasks.AddTask(new DocumentTask
            {
                DocumentId = 1,
                AuthorId = 1,
                ExecutorId = 2,
                Description = "Подготовить",
                Deadline = DateTime.Now.AddHours(-1),
                Status = DocumentTaskStatus.InProgress,
                CreatedAt = DateTime.Now.AddDays(-2),
            });

            _service.TickReminders(DateTime.Now);
            _service.TickReminders(DateTime.Now);
            var overdue = _repo.ListByRelatedTask(task.Id, NotificationKind.TaskOverdue);
            Assert.Single(overdue);
        }

        [Fact]
        public void TickReminders_skips_completed_tasks()
        {
            var task = _tasks.AddTask(new DocumentTask
            {
                DocumentId = 1,
                AuthorId = 1,
                ExecutorId = 2,
                Description = "x",
                Deadline = DateTime.Now.AddHours(-2),
                Status = DocumentTaskStatus.Completed,
                CompletedAt = DateTime.Now.AddHours(-1),
                CreatedAt = DateTime.Now.AddDays(-2),
            });

            _service.TickReminders(DateTime.Now);
            Assert.Empty(_repo.ListByRelatedTask(task.Id, NotificationKind.TaskOverdue));
        }

        [Fact]
        public void TickReminders_skips_distant_future_deadlines()
        {
            var task = _tasks.AddTask(new DocumentTask
            {
                DocumentId = 1,
                AuthorId = 1,
                ExecutorId = 2,
                Description = "x",
                Deadline = DateTime.Now.AddDays(7),
                Status = DocumentTaskStatus.New,
                CreatedAt = DateTime.Now,
            });

            _service.TickReminders(DateTime.Now);
            Assert.Empty(_repo.ListByRelatedTask(task.Id, NotificationKind.TaskDeadlineSoon));
        }

        [Fact]
        public void Create_throws_on_invalid_recipient_or_title()
        {
            Assert.Throws<ArgumentException>(() =>
                _service.Create(0, NotificationKind.System, "t", "b"));
            Assert.Throws<ArgumentException>(() =>
                _service.Create(1, NotificationKind.System, " ", "b"));
        }
    }
}
