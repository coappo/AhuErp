using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Абстракция доступа к сотрудникам. В проде привязывается к EF6,
    /// в тестах — к in-memory реализации (паттерн, уже применённый в Phase 1).
    /// </summary>
    public interface IEmployeeRepository
    {
        /// <summary>
        /// Возвращает сотрудника по уникальному ФИО (Phase 2 — упрощённая схема,
        /// в Phase 5 может быть заменена на логин/email). Возвращает <c>null</c>,
        /// если сотрудника нет.
        /// </summary>
        Employee FindByFullName(string fullName);

        /// <summary>Сотрудник по идентификатору; <c>null</c>, если нет.</summary>
        Employee GetById(int id);
    }
}
