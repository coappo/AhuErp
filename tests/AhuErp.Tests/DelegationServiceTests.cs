using System;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Phase 11 — поведение <see cref="DelegationService"/>:
    /// смена исполнителя поручения, журнал делегирований, аудит.
    /// </summary>
    public class DelegationServiceTests
    {
        private readonly InMemoryDocumentRepository _docs = new InMemoryDocumentRepository();
        private readonly InMemoryTaskRepository _tasksRepo = new InMemoryTaskRepository();
        private readonly InMemoryDelegationRepository _delegationRepo = new InMemoryDelegationRepository();
        private readonly InMemoryAuditLogRepository _auditRepo = new InMemoryAuditLogRepository();
        private readonly AuditService _audit;
        private readonly TaskService _taskService;
        private readonly DelegationService _service;
        private readonly Document _doc;

        public DelegationServiceTests()
        {
            _audit = new AuditService(_auditRepo);
            _taskService = new TaskService(_tasksRepo, _docs, _audit);
            _service = new DelegationService(_delegationRepo, _tasksRepo, _audit);
            _doc = new Document
            {
                Title = "Распоряжение для делегирования",
                Type = DocumentType.Internal,
                CreationDate = DateTime.Now.AddDays(-1),
                Deadline = DateTime.Now.AddDays(10),
            };
            _docs.Add(_doc);
        }

        private DocumentTask MakeTask(int executorId)
        {
            return _taskService.CreateTask(_doc.Id, authorId: 1,
                executorId: executorId, description: "Описание",
                deadline: DateTime.UtcNow.AddDays(5));
        }

        [Fact]
        public void Delegate_changes_executor_and_records_history()
        {
            var task = MakeTask(executorId: 2);
            var delegation = _service.Delegate(task.Id, toEmployeeId: 5, actorId: 1, comment: "В отпуске");

            Assert.Equal(5, _tasksRepo.GetTask(task.Id).ExecutorId);
            Assert.Equal(2, delegation.FromEmployeeId);
            Assert.Equal(5, delegation.ToEmployeeId);
        }

        [Fact]
        public void Delegate_writes_audit_record()
        {
            var task = MakeTask(executorId: 2);
            _service.Delegate(task.Id, 5, actorId: 1, "test");

            var logs = _audit.Query(new AuditQueryFilter { ActionType = AuditActionType.TaskDelegated });
            Assert.Single(logs);
            Assert.Equal(task.Id, logs[0].EntityId);
        }

        [Fact]
        public void Delegate_history_returns_records_in_chronological_order()
        {
            var task = MakeTask(executorId: 2);
            _service.Delegate(task.Id, 5, actorId: 1, "first");
            _service.Delegate(task.Id, 7, actorId: 1, "second");

            var history = _service.History(task.Id);
            Assert.Equal(2, history.Count);
            Assert.Equal("first", history[0].Comment);
            Assert.Equal("second", history[1].Comment);
        }

        [Fact]
        public void Delegate_rejects_same_employee()
        {
            var task = MakeTask(executorId: 2);
            Assert.Throws<InvalidOperationException>(() =>
                _service.Delegate(task.Id, 2, actorId: 1, "noop"));
        }

        [Fact]
        public void Delegate_rejects_completed_task()
        {
            var task = MakeTask(executorId: 2);
            _taskService.UpdateStatus(task.Id, DocumentTaskStatus.Completed, actorId: 2, reportText: "Готово");
            Assert.Throws<InvalidOperationException>(() =>
                _service.Delegate(task.Id, 5, actorId: 1, "поздно"));
        }

        [Fact]
        public void Delegate_rejects_unknown_task()
        {
            Assert.Throws<InvalidOperationException>(() =>
                _service.Delegate(taskId: 999, toEmployeeId: 5, actorId: 1, comment: null));
        }

        [Fact]
        public void Delegate_rejects_invalid_recipient()
        {
            var task = MakeTask(executorId: 2);
            Assert.Throws<ArgumentException>(() =>
                _service.Delegate(task.Id, toEmployeeId: 0, actorId: 1, comment: null));
        }
    }
}
