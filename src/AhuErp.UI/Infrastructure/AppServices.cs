using System;
using AhuErp.Core.Services;
using AhuErp.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace AhuErp.UI.Infrastructure
{
    /// <summary>
    /// Корневой композишн-рут. Регистрирует сервисы и ViewModel-ы в DI-контейнере
    /// и даёт к нему статический доступ из App.xaml.cs (строго как единый entry-point).
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

            // Демо-данные помещаем в уже существующие in-memory репозитории.
            var employees = (InMemoryEmployeeRepository)_provider.GetRequiredService<IEmployeeRepository>();
            var documents = (InMemoryDocumentRepository)_provider.GetRequiredService<IDocumentRepository>();
            var inventory = (InMemoryInventoryRepository)_provider.GetRequiredService<IInventoryRepository>();
            var hasher = _provider.GetRequiredService<IPasswordHasher>();
            DemoDataSeeder.Seed(employees, documents, hasher);
            DemoDataSeeder.SeedInventory(inventory);
        }

        public static T GetRequiredService<T>() where T : class =>
            Provider.GetRequiredService<T>();

        private static void ConfigureServices(IServiceCollection services)
        {
            // Core services
            services.AddSingleton<IPasswordHasher>(new Pbkdf2PasswordHasher(iterations: 10_000));
            services.AddSingleton<IEmployeeRepository>(new InMemoryEmployeeRepository());
            services.AddSingleton<IDocumentRepository>(new InMemoryDocumentRepository());
            services.AddSingleton<IAuthService, AuthService>();
            services.AddSingleton<ArchiveService>();
            services.AddSingleton<IInventoryRepository>(new InMemoryInventoryRepository());
            services.AddSingleton<IInventoryService, InventoryService>();

            // ViewModels — transient, чтобы получать свежее состояние при навигации.
            services.AddTransient<LoginViewModel>();
            services.AddTransient<MainViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<OfficeViewModel>();
            services.AddTransient<ArchiveViewModel>();
            services.AddTransient<ItServiceViewModel>();
            services.AddTransient<FleetViewModel>();
            services.AddTransient<WarehouseViewModel>();
        }
    }
}
