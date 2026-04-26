using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Декларативное сопоставление <see cref="EmployeeRole"/> и разрешённых
    /// модулей навигации. Используется и ViewModel-ем главного окна для
    /// фильтрации меню, и (в будущем) серверной авторизацией команд.
    /// </summary>
    public static class RolePolicy
    {
        public const string Dashboard = nameof(Dashboard);
        public const string Office = nameof(Office);
        public const string Archive = nameof(Archive);
        public const string ItService = nameof(ItService);
        public const string Fleet = nameof(Fleet);
        public const string Warehouse = nameof(Warehouse);

        // Phase 7 — модули, появляющиеся вместе с СЭД-функционалом.
        public const string MyTasks = nameof(MyTasks);
        public const string Nomenclature = nameof(Nomenclature);
        public const string AuditJournal = nameof(AuditJournal);

        // Phase 8 — журналы регистрации, поиск и отчётность СЭД.
        public const string Journals = nameof(Journals);
        public const string Search = nameof(Search);
        public const string Reports = nameof(Reports);

        private static readonly IReadOnlyDictionary<EmployeeRole, HashSet<string>> _allowed =
            new Dictionary<EmployeeRole, HashSet<string>>
            {
                [EmployeeRole.Admin] = new HashSet<string>
                {
                    Dashboard, Office, Archive, ItService, Fleet, Warehouse,
                    MyTasks, Nomenclature, AuditJournal,
                    Journals, Search, Reports
                },
                [EmployeeRole.Manager] = new HashSet<string>
                {
                    Dashboard, Office, Archive, ItService, Fleet, Warehouse,
                    MyTasks, Nomenclature,
                    Journals, Search, Reports
                },
                [EmployeeRole.Archivist] = new HashSet<string>
                {
                    Dashboard, Archive, MyTasks, Nomenclature,
                    Journals, Search, Reports
                },
                [EmployeeRole.TechSupport] = new HashSet<string>
                {
                    Dashboard, ItService, MyTasks, Search
                },
                [EmployeeRole.WarehouseManager] = new HashSet<string>
                {
                    Dashboard, Office, Fleet, Warehouse, MyTasks, Search, Reports
                },
            };

        /// <summary>
        /// True, если сотруднику с данной ролью виден модуль <paramref name="moduleKey"/>.
        /// Ключи — константы этого класса.
        /// </summary>
        public static bool IsAllowed(EmployeeRole role, string moduleKey)
        {
            return _allowed.TryGetValue(role, out var set) && set.Contains(moduleKey);
        }
    }
}
