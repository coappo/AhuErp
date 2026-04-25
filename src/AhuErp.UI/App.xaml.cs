using System;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using AhuErp.UI.Infrastructure;
using AhuErp.UI.ViewModels;

namespace AhuErp.UI
{
    /// <summary>
    /// Корневой App. Инициализирует DI-контейнер, показывает окно входа и,
    /// в случае успешной аутентификации, открывает <see cref="MainWindow"/>
    /// с уже разрешённой <see cref="MainViewModel"/> (зависимости приходят через DI).
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Любое необработанное исключение в WPF (в том числе при resolve
            // ViewModel-ов через DI и при запросах к EF6) иначе молча убивает
            // процесс. Показываем модалку с полным текстом, чтобы пользователь
            // мог переслать стек разработчику.
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

            AppServices.Initialize();

            var loginVm = AppServices.GetRequiredService<LoginViewModel>();
            var login = new LoginWindow(loginVm);

            if (login.ShowDialog() != true)
            {
                Shutdown();
                return;
            }

            var mainVm = AppServices.GetRequiredService<MainViewModel>();
            var main = new MainWindow { DataContext = mainVm };
            MainWindow = main;
            main.Show();
        }

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ShowFatal(e.Exception, "UI-поток");

            // Если падение случилось ДО показа главного окна (например, ошибка
            // подключения к SQL Server в EfDataSeeder или ошибка резолва
            // ViewModel-ов через DI), оставлять процесс «живым» нельзя: при
            // ShutdownMode.OnLastWindowClose ни одного окна нет — получится
            // невидимый зомби. Поэтому после показа стека сразу глушим процесс.
            if (Current?.MainWindow == null)
            {
                Current?.Shutdown(1);
                return;
            }

            // На штатной работе: помечаем обработанным, чтобы один сбой в кнопке
            // не убивал всё приложение.
            e.Handled = true;
        }

        private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                ShowFatal(ex, "AppDomain");
            }
        }

        private static void ShowFatal(Exception ex, string source)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Источник: {source}");
            sb.AppendLine();
            for (var current = ex; current != null; current = current.InnerException)
            {
                sb.AppendLine($"{current.GetType().FullName}: {current.Message}");
                sb.AppendLine(current.StackTrace);
                sb.AppendLine();
            }
            MessageBox.Show(
                sb.ToString(),
                "Необработанная ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
