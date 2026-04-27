using System;
using System.Collections.ObjectModel;
using System.Linq;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AhuErp.UI.ViewModels
{
    /// <summary>
    /// Phase 11 — раздел «Замещения». Позволяет создать новое замещение,
    /// отменить активное и просмотреть список всех замещений.
    /// </summary>
    public partial class SubstitutionsViewModel : ViewModelBase
    {
        private readonly ISubstitutionService _service;
        private readonly IAuthService _auth;
        private readonly IEmployeeRepository _employees;

        public ObservableCollection<Substitution> Items { get; } = new ObservableCollection<Substitution>();
        public ObservableCollection<Employee> Employees { get; } = new ObservableCollection<Employee>();

        public SubstitutionScope[] Scopes { get; } =
            (SubstitutionScope[])Enum.GetValues(typeof(SubstitutionScope));

        [ObservableProperty]
        private Employee selectedOriginal;

        [ObservableProperty]
        private Employee selectedSubstitute;

        [ObservableProperty]
        private DateTime fromDate = DateTime.Today;

        [ObservableProperty]
        private DateTime toDate = DateTime.Today.AddDays(7);

        [ObservableProperty]
        private SubstitutionScope scope = SubstitutionScope.Full;

        [ObservableProperty]
        private string reason;

        [ObservableProperty]
        private bool showActiveOnly;

        [ObservableProperty]
        private string errorMessage;

        public SubstitutionsViewModel(
            ISubstitutionService service,
            IAuthService auth,
            IEmployeeRepository employees)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _employees = employees ?? throw new ArgumentNullException(nameof(employees));

            ReloadEmployees();
            Reload();
        }

        partial void OnShowActiveOnlyChanged(bool value) => Reload();

        [RelayCommand]
        private void Reload()
        {
            Items.Clear();
            var rows = ShowActiveOnly
                ? _service.ListActive(DateTime.Now)
                : _service.ListAll();
            foreach (var s in rows) Items.Add(s);
        }

        [RelayCommand]
        private void Create()
        {
            ErrorMessage = null;
            try
            {
                if (SelectedOriginal == null || SelectedSubstitute == null)
                    throw new InvalidOperationException("Заполните обоих сотрудников.");
                if (_auth.CurrentEmployee == null)
                    throw new InvalidOperationException("Нет активного пользователя.");
                if (!RolePolicy.CanCreateSubstitution(_auth.CurrentEmployee.Role))
                    throw new UnauthorizedAccessException("У вас нет права создавать замещения.");

                _service.Create(
                    originalId: SelectedOriginal.Id,
                    substituteId: SelectedSubstitute.Id,
                    from: FromDate,
                    to: ToDate,
                    scope: Scope,
                    reason: Reason,
                    actorId: _auth.CurrentEmployee.Id);
                Reload();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        [RelayCommand]
        private void Cancel(Substitution row)
        {
            if (row == null) return;
            try
            {
                if (_auth.CurrentEmployee == null) return;
                _service.Cancel(row.Id, _auth.CurrentEmployee.Id);
                Reload();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        private void ReloadEmployees()
        {
            Employees.Clear();
            foreach (var e in _employees.ListAll().OrderBy(x => x.FullName))
            {
                Employees.Add(e);
            }
        }
    }
}
