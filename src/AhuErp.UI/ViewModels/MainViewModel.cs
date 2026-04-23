using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AhuErp.UI.ViewModels
{
    /// <summary>
    /// Корневая ViewModel. Управляет списком пунктов навигации и текущей
    /// активной страницей (<see cref="CurrentViewModel"/>).
    /// </summary>
    public partial class MainViewModel : ViewModelBase
    {
        public ObservableCollection<NavigationItem> NavigationItems { get; }

        [ObservableProperty]
        private NavigationItem selectedNavigationItem;

        [ObservableProperty]
        private ViewModelBase currentViewModel;

        public MainViewModel()
        {
            NavigationItems = new ObservableCollection<NavigationItem>
            {
                new NavigationItem("Дашборд", new DashboardViewModel()),
                new NavigationItem("Канцелярия", new OfficeViewModel()),
                new NavigationItem("Архив", new ArchiveViewModel()),
                new NavigationItem("IT-служба", new ItServiceViewModel()),
                new NavigationItem("Автопарк", new FleetViewModel()),
            };

            SelectedNavigationItem = NavigationItems[0];
        }

        partial void OnSelectedNavigationItemChanged(NavigationItem value)
        {
            CurrentViewModel = value?.ViewModel;
        }

        [RelayCommand]
        private void NavigateTo(NavigationItem item)
        {
            if (item != null) SelectedNavigationItem = item;
        }
    }
}
