using System;
using AhuErp.Core.Data;
using AhuErp.Core.Services;
using AhuErp.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace AhuErp.UI.Infrastructure
{
    /// <summary>
    /// Корневой композишн-рут. Регистрирует сервисы и ViewModel-ы в DI-контейнере
    /// и даёт к нему статический доступ из App.xaml.cs (строго как единый entry-point).
    /// Phase 6: репозитории работают через EF6 (<see cref="AhuDbContext"/>) поверх
    /// SQL Server, схема создаётся скриптом <c>scripts/create-db.sql</c>.
    /// </summary>
    public static class AppServices
    {
        private static IServiceProvider _provider;

        public static IServiceProvider Provider =>
            _provider ?? throw new InvalidOperationException(
                "AppServices не инициализирован. Вызовите AppServices.Initialize() перед использованием.");

        public static void Initialize()
        {
            if (_provider != null) return;

            var services = new ServiceCollection();
            ConfigureServices(services);
            _provider = services.BuildServiceProvider();

            // Минимальный сидинг — только администратор, чтобы можно было войти при
            // пустой БД (схема накатывается извне через scripts/create-db.sql).
            var ctx = _provider.GetRequiredService<AhuDbContext>();
            var hasher = _provider.GetRequiredService<IPasswordHasher>();
            EfDataSeeder.EnsureSeeded(ctx, hasher);
        }

        public static T GetRequiredService<T>() where T : class =>
            Provider.GetRequiredService<T>();

        private static void ConfigureServices(IServiceCollection services)
        {
            // EF6 контекст — singleton: WPF-приложение однопользовательское, обращения
            // идут с UI-потока (фоновые задачи делают снимок коллекций до Task.Run,
            // см. DashboardViewModel). Контекст-как-singleton избавляет от ручного
            // attach/detach при последовательных операциях над одной сущностью.
            services.AddSingleton<AhuDbContext>(sp => new AhuDbContext());

            // Core services — все репозитории теперь EF6, реализации In-Memory
            // остаются в кодовой базе для тестов (используются напрямую, не через DI).
            services.AddSingleton<IPasswordHasher>(new Pbkdf2PasswordHasher(iterations: 10_000));
            services.AddSingleton<IEmployeeRepository, EfEmployeeRepository>();
            services.AddSingleton<IDocumentRepository, EfDocumentRepository>();
            services.AddSingleton<IAuthService, AuthService>();
            services.AddSingleton<ArchiveService>();
            services.AddSingleton<IInventoryRepository, EfInventoryRepository>();
            services.AddSingleton<IInventoryService, InventoryService>();
            services.AddSingleton<IVehicleRepository, EfVehicleRepository>();
            services.AddSingleton<IFleetService>(sp => new FleetService(sp.GetRequiredService<IVehicleRepository>()));
            // ReportService: расширенный конструктор с EDMS-сервисами регистрируется
            // ниже, после ITaskService и INomenclatureRepository.

            // Phase 7: enterprise EDMS-сервисы. Все построены поверх единого
            // AhuDbContext-singleton, доступ из UI-потока.
            services.AddSingleton<IAuditLogRepository, EfAuditLogRepository>();
            services.AddSingleton<IAuditService, AuditService>();

            services.AddSingleton<INomenclatureRepository, EfNomenclatureRepository>();
            services.AddSingleton<INomenclatureService, NomenclatureService>();

            services.AddSingleton<IFileStorageService>(sp => new FileSystemStorageService());
            services.AddSingleton<IAttachmentRepository, EfAttachmentRepository>();
            services.AddSingleton<IAttachmentService, AttachmentService>();

            services.AddSingleton<ITaskRepository, EfTaskRepository>();
            services.AddSingleton<IWorkflowService, WorkflowService>();
            services.AddSingleton<ITaskService, TaskService>();

            services.AddSingleton<IApprovalRepository, EfApprovalRepository>();
            services.AddSingleton<IApprovalService, ApprovalService>();

            services.AddSingleton<IReportService>(sp => new ReportService(
                sp.GetRequiredService<IInventoryRepository>(),
                sp.GetRequiredService<IDocumentRepository>(),
                sp.GetRequiredService<ITaskService>(),
                sp.GetRequiredService<ITaskRepository>(),
                sp.GetRequiredService<INomenclatureRepository>()));

            // UI-инфраструктура
            services.AddSingleton<IFileDialogService, FileDialogService>();

            // ViewModels — transient, чтобы получать свежее состояние при навигации.
            services.AddTransient<LoginViewModel>();
            services.AddTransient<MainViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<OfficeViewModel>();
            services.AddTransient<ArchiveViewModel>();
            services.AddTransient<ItServiceViewModel>();
            services.AddTransient<FleetViewModel>();
            services.AddTransient<WarehouseViewModel>();
            services.AddTransient<RkkViewModel>();
            services.AddTransient<MyTasksViewModel>();
            services.AddTransient<NomenclatureViewModel>();
            services.AddTransient<AuditJournalViewModel>();
            services.AddTransient<JournalViewModel>();
            services.AddTransient<SearchViewModel>();
        }
    }
}
