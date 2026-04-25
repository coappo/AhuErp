using System;
using System.Globalization;
using System.Text;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Базовая реализация <see cref="IAuthService"/>. Держит текущего
    /// <see cref="Employee"/> в оперативной памяти; при хранении
    /// последней сессии (remember me) и аудите — расширять в Phase 5.
    /// </summary>
    public sealed class AuthService : IAuthService
    {
        private readonly IEmployeeRepository _employees;
        private readonly IPasswordHasher _hasher;

        public Employee CurrentEmployee { get; private set; }

        public bool IsAuthenticated => CurrentEmployee != null;

        public LoginFailureReason LastFailureReason { get; private set; } = LoginFailureReason.None;

        public AuthService(IEmployeeRepository employees, IPasswordHasher hasher)
        {
            _employees = employees ?? throw new ArgumentNullException(nameof(employees));
            _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        }

        public bool TryLogin(string fullName, string password)
        {
            CurrentEmployee = null;

            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrEmpty(password))
            {
                LastFailureReason = LoginFailureReason.EmptyInput;
                return false;
            }

            // Trim + Unicode-нормализация (NFC) защищает от случайно вставленного NBSP /
            // декомпозированных символов из буфера обмена. Кириллица в Windows обычно
            // приходит в NFC, но из браузеров/MacOS бывает NFD, и сравнение тогда падает.
            var normalized = NormalizeFullName(fullName);

            var employee = _employees.FindByFullName(normalized);
            if (employee == null)
            {
                LastFailureReason = LoginFailureReason.UserNotFound;
                return false;
            }
            if (string.IsNullOrEmpty(employee.PasswordHash))
            {
                LastFailureReason = LoginFailureReason.WrongPassword;
                return false;
            }

            if (!_hasher.Verify(password, employee.PasswordHash))
            {
                LastFailureReason = LoginFailureReason.WrongPassword;
                return false;
            }

            LastFailureReason = LoginFailureReason.None;
            CurrentEmployee = employee;
            return true;
        }

        public void Logout()
        {
            CurrentEmployee = null;
            LastFailureReason = LoginFailureReason.None;
        }

        private static string NormalizeFullName(string value)
        {
            var trimmed = value.Trim();
            try
            {
                return trimmed.Normalize(NormalizationForm.FormC);
            }
            catch (ArgumentException)
            {
                // На неконвертируемых последовательностях возвращаем хоть что-то,
                // чтобы поиск отработал, а не валился исключением.
                return trimmed;
            }
        }
    }
}
