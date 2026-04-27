using System;
using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Сервис управления замещениями (Phase 11). Резолвит фактического
    /// исполнителя для поручений и согласований с учётом активного замещения.
    /// </summary>
    public interface ISubstitutionService
    {
        /// <summary>
        /// Создать замещение. Бросает <see cref="InvalidOperationException"/>
        /// при пересечении интервалов с уже существующим активным замещением
        /// для того же <paramref name="originalId"/>.
        /// </summary>
        Substitution Create(int originalId, int substituteId, DateTime from,
                            DateTime to, SubstitutionScope scope, string reason, int actorId);

        /// <summary>Отменить замещение. Идемпотентно: повторный вызов — без эффекта.</summary>
        void Cancel(int id, int actorId);

        /// <summary>
        /// Активное замещение для сотрудника на момент <paramref name="now"/> и
        /// в области <paramref name="scope"/>; null — если замещения нет.
        /// </summary>
        Substitution GetActiveSubstitute(int employeeId, DateTime now, SubstitutionScope scope);

        /// <summary>Все активные замещения (для UI «кого я замещаю / кто замещает меня»).</summary>
        IReadOnlyList<Substitution> ListActive(DateTime now);

        /// <summary>Все замещения (для журналов и отчётности).</summary>
        IReadOnlyList<Substitution> ListAll();

        /// <summary>
        /// Если для сотрудника есть активное замещение в области
        /// <paramref name="scope"/> — возвращает Id заместителя, иначе сам Id.
        /// Используется TaskService.CreateTask и ApprovalService.StartApproval.
        /// </summary>
        int ResolveActualExecutor(int employeeId, DateTime now, SubstitutionScope scope);
    }
}
