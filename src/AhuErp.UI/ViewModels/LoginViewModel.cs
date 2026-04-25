using System;
using AhuErp.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AhuErp.UI.ViewModels
{
    /// <summary>
    /// ViewModel окна входа. Отвечает только за валидацию ввода и делегирование
    /// проверки пароля в <see cref="IAuthService"/>.
    /// </summary>
    public partial class LoginViewModel : ViewModelBase
    {
        private readonly IAuthService _auth;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
        private string fullName;

        [ObservableProperty]
        private string errorMessage;

        /// <summary>
        /// True, если вход успешно состоялся. Окно входа закрывается по этому флагу.
        /// </summary>
        [ObservableProperty]
        private bool isAuthenticated;

        public LoginViewModel(IAuthService auth)
        {
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
        }

        /// <summary>
        /// Пароль передаётся из View (PasswordBox не поддерживает двустороннюю
        /// привязку из соображений безопасности) — поэтому принимаем его
        /// параметром команды.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanLogin))]
        private void Login(object passwordBox)
        {
            ErrorMessage = null;
            var password = passwordBox as string ?? string.Empty;

            if (!_auth.TryLogin(FullName, password))
            {
                // Разделение причин помогает локализовать проблему при первом запуске
                // на новой БД: «не найден» — обычно опечатка/лишние пробелы в ФИО,
                // «неверный пароль» — раскладка/CapsLock либо реально не тот пароль.
                switch (_auth.LastFailureReason)
                {
                    case LoginFailureReason.EmptyInput:
                        ErrorMessage = "Введите ФИО и пароль.";
                        break;
                    case LoginFailureReason.UserNotFound:
                        ErrorMessage = "Пользователь с таким ФИО не найден.";
                        break;
                    case LoginFailureReason.WrongPassword:
                        ErrorMessage = "Неверный пароль.";
                        break;
                    default:
                        ErrorMessage = "Неверное ФИО или пароль.";
                        break;
                }
                IsAuthenticated = false;
                return;
            }

            IsAuthenticated = true;
        }

        private bool CanLogin(object _) => !string.IsNullOrWhiteSpace(FullName);
    }
}
