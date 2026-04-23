using System;
using System.Collections.ObjectModel;
using AhuErp.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AhuErp.UI.ViewModels
{
    /// <summary>
    /// Корневая ViewModel. Содержит список пунктов навигации, фильтрует их
    /// по роли текущего пользователя (<see cref="RolePolicy"/>) и управляет
    /// активной подстраницей <see cref="CurrentViewModel"/>.
    /// </summary>
    public partial class MainViewModel : ViewModelBase
    {
        private readonly IAuthService _auth;

        public ObservableCollection<NavigationItem> NavigationItems { get; }

        [ObservableProperty]
        private NavigationItem selectedNavigationItem;

        [ObservableProperty]
        private ViewModelBase currentViewModel;

        [ObservableProperty]
        private string currentUserDisplayName;

        [ObservableProperty]
        private string currentUserRoleDisplayName;

        public MainViewModel(IAuthService auth,
                             DashboardViewModel dashboardVm,
                             OfficeViewModel officeVm,
                             ArchiveViewModel archiveVm,
                             ItServiceViewModel itServiceVm,
                             FleetViewModel fleetVm,
                             WarehouseViewModel warehouseVm)
        {
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));

            NavigationItems = new ObservableCollection<NavigationItem>
            {
                new NavigationItem("Дашборд",    RolePolicy.Dashboard, dashboardVm),
                new NavigationItem("Канцелярия", RolePolicy.Office,    officeVm),
                new NavigationItem("Архив",      RolePolicy.Archive,   archiveVm),
                new NavigationItem("Склад / ТМЦ", RolePolicy.Warehouse, warehouseVm),
                new NavigationItem("IT-служба",  RolePolicy.ItService, itServiceVm),
                new NavigationItem("Автопарк",   RolePolicy.Fleet,     fleetVm),
            };

            ApplyRolePolicy();

            // Выбираем первый доступный пункт.
            foreach (var item in NavigationItems)
            {
                if (item.IsAllowed)
                {
                    SelectedNavigationItem = item;
                    break;
                }
            }
        }

        partial void OnSelectedNavigationItemChanged(NavigationItem value)
        {
            CurrentViewModel = value?.ViewModel;
        }

        [RelayCommand]
        private void NavigateTo(NavigationItem item)
        {
            if (item != null && item.IsAllowed) SelectedNavigationItem = item;
        }

        [RelayCommand]
        private void Logout()
        {
            _auth.Logout();
            System.Windows.Application.Current.Shutdown();
        }

        private void ApplyRolePolicy()
        {
            var employee = _auth.CurrentEmployee;
            if (employee == null)
            {
                foreach (var item in NavigationItems) item.IsAllowed = false;
                CurrentUserDisplayName = null;
                CurrentUserRoleDisplayName = null;
                return;
            }

            foreach (var item in NavigationItems)
            {
                item.IsAllowed = RolePolicy.IsAllowed(employee.Role, item.ModuleKey);
            }

            CurrentUserDisplayName = employee.FullName;
            CurrentUserRoleDisplayName = employee.Role.ToString();
        }
    }
}
