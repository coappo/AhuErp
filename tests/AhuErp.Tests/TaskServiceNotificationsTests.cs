using System;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Phase 9 — интеграция <see cref="TaskService"/> + <see cref="NotificationService"/>:
    /// при создании поручения исполнителю должно прилететь in-app уведомление.
    /// </summary>
    public class TaskServiceNotificationsTests
    {
        [Fact]
        public void CreateTask_sends_TaskAssigned_to_executor()
        {
            var docs = new InMemoryDocumentRepository();
            var tasks = new InMemoryTaskRepository();
            var auditRepo = new InMemoryAuditLogRepository();
            var notifRepo = new InMemoryNotificationRepository();
            var employees = new InMemoryEmployeeRepository(new[]
            {
                new Employee { Id = 1, FullName = "Author", Role = EmployeeRole.Manager },
                new Employee { Id = 2, FullName = "Exec",   Role = EmployeeRole.TechSupport, Email = "exec@bmr" },
            });
            var audit = new AuditService(auditRepo);
            var notifications = new NotificationService(notifRepo, employees, tasks, audit);
            var service = new TaskService(tasks, docs, audit,
                workflow: null, substitution: null, delegations: null,
                notifications: notifications);

            var doc = new Document
            {
                Title = "Сл. зап.",
                Type = DocumentType.Internal,
                CreationDate = DateTime.Now.AddDays(-1),
                Deadline = DateTime.Now.AddDays(10),
            };
            docs.Add(doc);

            service.CreateTask(doc.Id, authorId: 1, executorId: 2,
                description: "Подготовить", deadline: DateTime.Now.AddDays(3));

            var inbox = notifications.ListForUser(2, unreadOnly: true);
            Assert.Single(inbox);
            Assert.Equal(NotificationKind.TaskAssigned, inbox[0].Kind);
            Assert.Equal(doc.Id, inbox[0].RelatedDocumentId);
        }
    }
}
