using System;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Phase 11 — интеграция <see cref="TaskService"/> и <see cref="SubstitutionService"/>:
    /// при наличии активного замещения CreateTask должен реально создать
    /// поручение на заместителя и зафиксировать делегирование.
    /// </summary>
    public class TaskServiceSubstitutionTests
    {
        [Fact]
        public void CreateTask_redirects_executor_when_substitution_is_active()
        {
            var docs = new InMemoryDocumentRepository();
            var tasksRepo = new InMemoryTaskRepository();
            var auditRepo = new InMemoryAuditLogRepository();
            var subRepo = new InMemorySubstitutionRepository();
            var delegationRepo = new InMemoryDelegationRepository();
            var audit = new AuditService(auditRepo);
            var sub = new SubstitutionService(subRepo, audit);
            var service = new TaskService(tasksRepo, docs, audit, workflow: null,
                substitution: sub, delegations: delegationRepo);

            var doc = new Document
            {
                Title = "СЗ",
                Type = DocumentType.Internal,
                CreationDate = DateTime.Now.AddDays(-1),
                Deadline = DateTime.Now.AddDays(10),
            };
            docs.Add(doc);

            sub.Create(originalId: 2, substituteId: 9,
                from: DateTime.Today.AddDays(-1), to: DateTime.Today.AddDays(1),
                scope: SubstitutionScope.Full, reason: null, actorId: 1);

            var task = service.CreateTask(doc.Id, authorId: 1, executorId: 2,
                description: "Подготовить", deadline: DateTime.UtcNow.AddDays(3));

            Assert.Equal(9, task.ExecutorId);
            var logs = audit.Query(new AuditQueryFilter { ActionType = AuditActionType.TaskDelegated });
            Assert.Single(logs);
            var history = delegationRepo.ListByTask(task.Id);
            Assert.Single(history);
            Assert.Equal(2, history[0].FromEmployeeId);
            Assert.Equal(9, history[0].ToEmployeeId);
        }

        [Fact]
        public void CreateTask_keeps_executor_when_only_approvals_substitution_is_active()
        {
            var docs = new InMemoryDocumentRepository();
            var tasksRepo = new InMemoryTaskRepository();
            var auditRepo = new InMemoryAuditLogRepository();
            var subRepo = new InMemorySubstitutionRepository();
            var delegationRepo = new InMemoryDelegationRepository();
            var audit = new AuditService(auditRepo);
            var sub = new SubstitutionService(subRepo, audit);
            var service = new TaskService(tasksRepo, docs, audit, workflow: null,
                substitution: sub, delegations: delegationRepo);

            var doc = new Document
            {
                Title = "СЗ",
                Type = DocumentType.Internal,
                CreationDate = DateTime.Now.AddDays(-1),
                Deadline = DateTime.Now.AddDays(10),
            };
            docs.Add(doc);

            sub.Create(originalId: 2, substituteId: 9,
                from: DateTime.Today.AddDays(-1), to: DateTime.Today.AddDays(1),
                scope: SubstitutionScope.ApprovalsOnly, reason: null, actorId: 1);

            var task = service.CreateTask(doc.Id, authorId: 1, executorId: 2,
                description: "Подготовить", deadline: DateTime.UtcNow.AddDays(3));

            Assert.Equal(2, task.ExecutorId);
            var logs = audit.Query(new AuditQueryFilter { ActionType = AuditActionType.TaskDelegated });
            Assert.Empty(logs);
        }
    }
}
