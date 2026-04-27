using System;
using System.Linq;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Phase 11 — поведение <see cref="SubstitutionService"/>:
    /// создание/отмена замещения, контроль перекрытий, резолв фактического
    /// исполнителя по областям TasksOnly/ApprovalsOnly/Full.
    /// </summary>
    public class SubstitutionServiceTests
    {
        private readonly InMemorySubstitutionRepository _repo = new InMemorySubstitutionRepository();
        private readonly InMemoryAuditLogRepository _auditRepo = new InMemoryAuditLogRepository();
        private readonly AuditService _audit;
        private readonly SubstitutionService _service;

        public SubstitutionServiceTests()
        {
            _audit = new AuditService(_auditRepo);
            _service = new SubstitutionService(_repo, _audit);
        }

        [Fact]
        public void Create_persists_entity_and_writes_audit()
        {
            var s = _service.Create(originalId: 1, substituteId: 2,
                from: DateTime.Today, to: DateTime.Today.AddDays(5),
                scope: SubstitutionScope.Full, reason: "Отпуск", actorId: 99);

            Assert.True(s.Id > 0);
            Assert.True(s.IsActive);
            var logs = _audit.Query(new AuditQueryFilter { ActionType = AuditActionType.SubstitutionCreated });
            Assert.Single(logs);
            Assert.Equal(s.Id, logs[0].EntityId);
        }

        [Fact]
        public void Create_rejects_self_substitution()
        {
            Assert.Throws<InvalidOperationException>(() =>
                _service.Create(1, 1, DateTime.Today, DateTime.Today.AddDays(1),
                                SubstitutionScope.Full, null, actorId: 1));
        }

        [Fact]
        public void Create_rejects_inverted_dates()
        {
            Assert.Throws<ArgumentException>(() =>
                _service.Create(1, 2, DateTime.Today.AddDays(5), DateTime.Today,
                                SubstitutionScope.TasksOnly, null, actorId: 1));
        }

        [Fact]
        public void Create_rejects_overlapping_active_substitution_for_same_employee()
        {
            _service.Create(1, 2, DateTime.Today, DateTime.Today.AddDays(5),
                            SubstitutionScope.TasksOnly, null, actorId: 1);

            Assert.Throws<InvalidOperationException>(() =>
                _service.Create(1, 3, DateTime.Today.AddDays(3), DateTime.Today.AddDays(8),
                                SubstitutionScope.TasksOnly, null, actorId: 1));
        }

        [Fact]
        public void Create_allows_non_overlapping_scopes()
        {
            // TasksOnly не пересекается с ApprovalsOnly — две записи допустимы.
            _service.Create(1, 2, DateTime.Today, DateTime.Today.AddDays(5),
                            SubstitutionScope.TasksOnly, null, actorId: 1);
            var second = _service.Create(1, 3, DateTime.Today, DateTime.Today.AddDays(5),
                                         SubstitutionScope.ApprovalsOnly, null, actorId: 1);
            Assert.True(second.Id > 0);
        }

        [Fact]
        public void Cancel_marks_inactive_and_writes_audit()
        {
            var s = _service.Create(1, 2, DateTime.Today, DateTime.Today.AddDays(5),
                                    SubstitutionScope.Full, null, actorId: 1);
            _service.Cancel(s.Id, actorId: 1);
            Assert.False(_repo.Get(s.Id).IsActive);
            var logs = _audit.Query(new AuditQueryFilter { ActionType = AuditActionType.SubstitutionCancelled });
            Assert.Single(logs);
        }

        [Fact]
        public void Cancel_is_idempotent_for_already_cancelled()
        {
            var s = _service.Create(1, 2, DateTime.Today, DateTime.Today.AddDays(5),
                                    SubstitutionScope.Full, null, actorId: 1);
            _service.Cancel(s.Id, actorId: 1);
            _service.Cancel(s.Id, actorId: 1); // не должно бросить
            var logs = _audit.Query(new AuditQueryFilter { ActionType = AuditActionType.SubstitutionCancelled });
            Assert.Single(logs);
        }

        [Fact]
        public void ResolveActualExecutor_returns_original_when_no_substitution()
        {
            int actual = _service.ResolveActualExecutor(employeeId: 7, DateTime.Now,
                                                       SubstitutionScope.TasksOnly);
            Assert.Equal(7, actual);
        }

        [Fact]
        public void ResolveActualExecutor_returns_substitute_when_full_scope_active()
        {
            _service.Create(originalId: 7, substituteId: 8, from: DateTime.Today.AddDays(-1),
                            to: DateTime.Today.AddDays(1), scope: SubstitutionScope.Full,
                            reason: null, actorId: 1);

            int actualTasks = _service.ResolveActualExecutor(7, DateTime.Now, SubstitutionScope.TasksOnly);
            int actualApprovals = _service.ResolveActualExecutor(7, DateTime.Now, SubstitutionScope.ApprovalsOnly);
            Assert.Equal(8, actualTasks);
            Assert.Equal(8, actualApprovals);
        }

        [Fact]
        public void ResolveActualExecutor_respects_scope_partition()
        {
            // TasksOnly-замещение не должно влиять на согласования.
            _service.Create(7, 8, DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1),
                            SubstitutionScope.TasksOnly, null, actorId: 1);

            Assert.Equal(8, _service.ResolveActualExecutor(7, DateTime.Now, SubstitutionScope.TasksOnly));
            Assert.Equal(7, _service.ResolveActualExecutor(7, DateTime.Now, SubstitutionScope.ApprovalsOnly));
        }

        [Fact]
        public void ResolveActualExecutor_ignores_expired_substitution()
        {
            _service.Create(7, 8, DateTime.Today.AddDays(-10), DateTime.Today.AddDays(-5),
                            SubstitutionScope.Full, null, actorId: 1);

            Assert.Equal(7, _service.ResolveActualExecutor(7, DateTime.Now, SubstitutionScope.TasksOnly));
        }

        [Fact]
        public void ResolveActualExecutor_ignores_cancelled_substitution()
        {
            var s = _service.Create(7, 8, DateTime.Today, DateTime.Today.AddDays(5),
                                    SubstitutionScope.Full, null, actorId: 1);
            _service.Cancel(s.Id, actorId: 1);

            Assert.Equal(7, _service.ResolveActualExecutor(7, DateTime.Now, SubstitutionScope.TasksOnly));
        }

        [Fact]
        public void ListActive_returns_only_currently_active()
        {
            _service.Create(1, 2, DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1),
                            SubstitutionScope.Full, null, actorId: 1); // активно
            _service.Create(3, 4, DateTime.Today.AddDays(5), DateTime.Today.AddDays(10),
                            SubstitutionScope.Full, null, actorId: 1); // ещё не началось
            _service.Create(5, 6, DateTime.Today.AddDays(-10), DateTime.Today.AddDays(-5),
                            SubstitutionScope.Full, null, actorId: 1); // уже прошло

            var active = _service.ListActive(DateTime.Now);
            Assert.Single(active);
            Assert.Equal(1, active[0].OriginalEmployeeId);
        }

        [Fact]
        public void ListAll_returns_every_substitution_regardless_of_state()
        {
            _service.Create(1, 2, DateTime.Today, DateTime.Today.AddDays(1),
                            SubstitutionScope.Full, null, actorId: 1);
            var s2 = _service.Create(3, 4, DateTime.Today, DateTime.Today.AddDays(1),
                                     SubstitutionScope.Full, null, actorId: 1);
            _service.Cancel(s2.Id, actorId: 1);

            Assert.Equal(2, _service.ListAll().Count);
        }
    }
}
