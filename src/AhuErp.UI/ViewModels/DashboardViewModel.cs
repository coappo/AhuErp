using CommunityToolkit.Mvvm.ComponentModel;

namespace AhuErp.UI.ViewModels
{
    public partial class DashboardViewModel : ViewModelBase
    {
        [ObservableProperty]
        private int overdueCount;

        [ObservableProperty]
        private int dueSoonCount;

        [ObservableProperty]
        private int activeVehicles;

        public DashboardViewModel()
        {
            // В Phase 1 используем заглушечные данные — на следующих фазах сюда
            // придёт IDashboardService, работающий через AhuDbContext.
            OverdueCount = 0;
            DueSoonCount = 0;
            ActiveVehicles = 0;
        }
    }
}
