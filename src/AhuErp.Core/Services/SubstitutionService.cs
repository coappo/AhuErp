using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Реализация <see cref="ISubstitutionService"/>. Все мутации идут в журнал
    /// аудита (<see cref="AuditActionType.SubstitutionCreated"/> /
    /// <see cref="AuditActionType.SubstitutionCancelled"/>).
    /// </summary>
    public sealed class SubstitutionService : ISubstitutionService
    {
        private readonly ISubstitutionRepository _repository;
        private readonly IAuditService _audit;

        public SubstitutionService(ISubstitutionRepository repository, IAuditService audit)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        }

        public Substitution Create(int originalId, int substituteId, DateTime from,
                                   DateTime to, SubstitutionScope scope, string reason, int actorId)
        {
            if (originalId <= 0) throw new ArgumentException("OriginalEmployeeId обязателен.", nameof(originalId));
            if (substituteId <= 0) throw new ArgumentException("SubstituteEmployeeId обязателен.", nameof(substituteId));
            if (originalId == substituteId)
                throw new InvalidOperationException("Сотрудник не может замещать сам себя.");
            if (to < from)
                throw new ArgumentException("Дата окончания не может быть раньше даты начала.", nameof(to));

            // Запрет перекрытий: для одного OriginalEmployee два активных замещения
            // одной и той же области не должны пересекаться по времени, иначе
            // ResolveActualExecutor вернёт неопределённый результат.
            var existing = _repository.ListByOriginal(originalId);
            foreach (var s in existing)
            {
                if (!s.IsActive) continue;
                if (!ScopesOverlap(s.Scope, scope)) continue;
                if (IntervalsOverlap(s.From, s.To, from, to))
                    throw new InvalidOperationException(
                        $"Замещение пересекается с активным #{s.Id} ({s.From:d}–{s.To:d}, область {s.Scope}).");
            }

            var entity = _repository.Add(new Substitution
            {
                OriginalEmployeeId = originalId,
                SubstituteEmployeeId = substituteId,
                From = from,
                To = to,
                Scope = scope,
                Reason = reason,
                IsActive = true,
                CreatedById = actorId
            });

            _audit.Record(AuditActionType.SubstitutionCreated, nameof(Substitution), entity.Id, actorId,
                newValues: $"Original={originalId}; Substitute={substituteId}; " +
                           $"From={from:o}; To={to:o}; Scope={scope}");
            return entity;
        }

        public void Cancel(int id, int actorId)
        {
            var entity = _repository.Get(id)
                ?? throw new InvalidOperationException($"Замещение #{id} не найдено.");
            if (!entity.IsActive) return;
            entity.IsActive = false;
            _repository.Update(entity);
            _audit.Record(AuditActionType.SubstitutionCancelled, nameof(Substitution), entity.Id, actorId,
                oldValues: "IsActive=true", newValues: "IsActive=false");
        }

        public Substitution GetActiveSubstitute(int employeeId, DateTime now, SubstitutionScope scope)
        {
            return _repository.ListActive(now)
                .FirstOrDefault(s => s.OriginalEmployeeId == employeeId
                                     && ScopesOverlap(s.Scope, scope));
        }

        public IReadOnlyList<Substitution> ListActive(DateTime now) => _repository.ListActive(now);

        public IReadOnlyList<Substitution> ListAll() => _repository.ListAll();

        public int ResolveActualExecutor(int employeeId, DateTime now, SubstitutionScope scope)
        {
            var s = GetActiveSubstitute(employeeId, now, scope);
            return s?.SubstituteEmployeeId ?? employeeId;
        }

        /// <summary>
        /// True, если запись с областью <paramref name="recorded"/> «покрывает»
        /// запрос с областью <paramref name="requested"/>. Full покрывает всё;
        /// TasksOnly/ApprovalsOnly — только свою специфику.
        /// </summary>
        private static bool ScopesOverlap(SubstitutionScope recorded, SubstitutionScope requested)
        {
            if (recorded == SubstitutionScope.Full || requested == SubstitutionScope.Full) return true;
            return recorded == requested;
        }

        private static bool IntervalsOverlap(DateTime aFrom, DateTime aTo, DateTime bFrom, DateTime bTo)
            => aFrom <= bTo && bFrom <= aTo;
    }
}
