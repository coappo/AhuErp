using System;
using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Реализация <see cref="IDelegationService"/>. Меняет исполнителя
    /// у <see cref="DocumentTask"/> и фиксирует историю в <see cref="TaskDelegation"/>.
    /// </summary>
    public sealed class DelegationService : IDelegationService
    {
        private readonly IDelegationRepository _repository;
        private readonly ITaskRepository _tasks;
        private readonly IAuditService _audit;

        public DelegationService(
            IDelegationRepository repository,
            ITaskRepository tasks,
            IAuditService audit)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        }

        public TaskDelegation Delegate(int taskId, int toEmployeeId, int actorId, string comment)
        {
            if (toEmployeeId <= 0)
                throw new ArgumentException("Получатель обязателен.", nameof(toEmployeeId));

            var task = _tasks.GetTask(taskId)
                ?? throw new InvalidOperationException($"Поручение #{taskId} не найдено.");
            if (task.ExecutorId == toEmployeeId)
                throw new InvalidOperationException("Поручение уже принадлежит этому сотруднику.");
            if (task.Status == DocumentTaskStatus.Completed
                || task.Status == DocumentTaskStatus.Cancelled)
                throw new InvalidOperationException("Нельзя делегировать завершённое поручение.");

            var fromEmployeeId = task.ExecutorId;
            task.ExecutorId = toEmployeeId;
            _tasks.UpdateTask(task);

            var entity = _repository.Add(new TaskDelegation
            {
                TaskId = taskId,
                FromEmployeeId = fromEmployeeId,
                ToEmployeeId = toEmployeeId,
                DelegatedAt = DateTime.Now,
                Comment = comment
            });

            _audit.Record(AuditActionType.TaskDelegated, nameof(DocumentTask), taskId, actorId,
                oldValues: $"Executor={fromEmployeeId}",
                newValues: $"Executor={toEmployeeId}; DelegationId={entity.Id}");
            return entity;
        }

        public IReadOnlyList<TaskDelegation> History(int taskId) => _repository.ListByTask(taskId);
    }
}
