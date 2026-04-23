using System;
using System.Linq;
using System.Threading.Tasks;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveCharts;
using LiveCharts.Wpf;

namespace AhuErp.UI.ViewModels
{
    /// <summary>
    /// Дашборд руководителя: KPI-карточки + Pie (статусы документов) + Bar (ТМЦ по категориям).
    /// Данные собираются в пуле потоков (<see cref="Task.Run"/>), чтобы открытие
    /// дашборда не блокировало UI-поток при большом количестве записей.
    /// </summary>
    public partial class DashboardViewModel : ViewModelBase
    {
        private const int LowStockThreshold = 5;

        private readonly IDocumentRepository _documents;
        private readonly IInventoryRepository _inventory;
        private readonly IVehicleRepository _vehicles;

        [ObservableProperty]
        private int overdueCount;

        [ObservableProperty]
        private int dueSoonCount;

        [ObservableProperty]
        private int activeVehicles;

        [ObservableProperty]
        private int overdueArchiveRequests;

        [ObservableProperty]
        private int lowStockItems;

        [ObservableProperty]
        private bool isLoading;

        [ObservableProperty]
        private string lastRefreshedDisplay;

        [ObservableProperty]
        private SeriesCollection documentStatusSeries = new SeriesCollection();

        [ObservableProperty]
        private SeriesCollection inventoryByCategorySeries = new SeriesCollection();

        [ObservableProperty]
        private string[] inventoryCategoryLabels = System.Array.Empty<string>();

        public DashboardViewModel(IDocumentRepository documents,
                                  IInventoryRepository inventory,
                                  IVehicleRepository vehicles)
        {
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _vehicles = vehicles ?? throw new ArgumentNullException(nameof(vehicles));

            // Безопасный запуск — не ждём Task, ошибки превращаем в no-op на уровне UI.
            _ = RefreshAsync();
        }

        [RelayCommand]
        private Task Refresh() => RefreshAsync();

        private async Task RefreshAsync()
        {
            IsLoading = true;
            try
            {
                var snapshot = await Task.Run(() => ComputeSnapshot()).ConfigureAwait(true);
                ApplySnapshot(snapshot);
                LastRefreshedDisplay = $"Обновлено: {DateTime.Now:HH:mm:ss}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private DashboardSnapshot ComputeSnapshot()
        {
            var now = DateTime.Now;

            var allDocuments =
                _documents.ListByType(DocumentType.Office)
                    .Concat(_documents.ListByType(DocumentType.Incoming))
                    .Concat(_documents.ListByType(DocumentType.Internal))
                    .Concat(_documents.ListByType(DocumentType.Archive))
                    .Concat(_documents.ListByType(DocumentType.It))
                    .Concat(_documents.ListByType(DocumentType.Fleet))
                    .Concat(_documents.ListByType(DocumentType.ArchiveRequest))
                    .Concat(_documents.ListByType(DocumentType.General))
                    .Concat(_documents.ListArchiveRequests().Cast<Document>())
                    .Concat(_documents.ListItTickets().Cast<Document>())
                    .GroupBy(d => d.Id)
                    .Select(g => g.First())
                    .ToList();

            var overdueAll = allDocuments.Count(d => d.IsOverdue(now));
            var dueSoon = allDocuments.Count(d =>
                d.Status != DocumentStatus.Completed
                && d.Status != DocumentStatus.Cancelled
                && d.Deadline >= now
                && d.Deadline <= now.AddDays(3));

            var archive = _documents.ListArchiveRequests();
            var overdueArchive = archive.Count(d => d.IsOverdue(now));

            var items = _inventory.ListItems();
            var lowStock = items.Count(i => i.TotalQuantity < LowStockThreshold);

            var trips = _vehicles.ListVehicles()
                .SelectMany(v => _vehicles.ListTrips(v.Id))
                .ToList();
            var onMission = trips.Count(t => t.StartDate <= now && t.EndDate > now);

            var statusGroups = allDocuments
                .GroupBy(d => d.Status)
                .Select(g => new StatusSlice(g.Key.ToString(), g.Count()))
                .OrderBy(x => x.Label, StringComparer.Ordinal)
                .ToArray();

            var categoryGroups = items
                .GroupBy(i => i.Category)
                .Select(g => new CategorySlice(g.Key.ToString(), g.Sum(i => i.TotalQuantity)))
                .OrderBy(x => x.Label, StringComparer.Ordinal)
                .ToArray();

            return new DashboardSnapshot(
                overdueAll, dueSoon, onMission, overdueArchive, lowStock,
                statusGroups, categoryGroups);
        }

        /// <summary>
        /// Применяет данные на UI-потоке. Принципиально: <see cref="PieSeries"/> и
        /// <see cref="ColumnSeries"/> наследуются от <c>FrameworkElement</c> и
        /// должны создаваться только из UI-диспетчера — поэтому мы строим их здесь,
        /// а не в <see cref="ComputeSnapshot"/>, который выполняется в пуле потоков.
        /// </summary>
        private void ApplySnapshot(DashboardSnapshot s)
        {
            OverdueCount = s.Overdue;
            DueSoonCount = s.DueSoon;
            ActiveVehicles = s.ActiveVehicles;
            OverdueArchiveRequests = s.OverdueArchiveRequests;
            LowStockItems = s.LowStockItems;

            var pie = new SeriesCollection();
            foreach (var slice in s.StatusGroups)
            {
                pie.Add(new PieSeries
                {
                    Title = slice.Label,
                    Values = new ChartValues<int> { slice.Count },
                    DataLabels = true,
                    LabelPoint = p => $"{p.SeriesView.Title}: {p.Y}"
                });
            }
            DocumentStatusSeries = pie;

            InventoryByCategorySeries = new SeriesCollection
            {
                new ColumnSeries
                {
                    Title = "Остаток, шт.",
                    Values = new ChartValues<int>(s.CategoryGroups.Select(c => c.Count)),
                    DataLabels = true
                }
            };
            InventoryCategoryLabels = s.CategoryGroups.Select(c => c.Label).ToArray();
        }

        private readonly struct StatusSlice
        {
            public string Label { get; }
            public int Count { get; }
            public StatusSlice(string label, int count) { Label = label; Count = count; }
        }

        private readonly struct CategorySlice
        {
            public string Label { get; }
            public int Count { get; }
            public CategorySlice(string label, int count) { Label = label; Count = count; }
        }

        private sealed class DashboardSnapshot
        {
            public int Overdue { get; }
            public int DueSoon { get; }
            public int ActiveVehicles { get; }
            public int OverdueArchiveRequests { get; }
            public int LowStockItems { get; }
            public StatusSlice[] StatusGroups { get; }
            public CategorySlice[] CategoryGroups { get; }

            public DashboardSnapshot(int overdue, int dueSoon, int active,
                                     int overdueArchive, int lowStock,
                                     StatusSlice[] status, CategorySlice[] category)
            {
                Overdue = overdue;
                DueSoon = dueSoon;
                ActiveVehicles = active;
                OverdueArchiveRequests = overdueArchive;
                LowStockItems = lowStock;
                StatusGroups = status;
                CategoryGroups = category;
            }
        }
    }
}
