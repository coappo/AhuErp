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
    /// Глобальный поиск по документам (заголовок, краткое содержание,
    /// рег.номер, корреспондент, входящий номер). Поддерживает быстрый
    /// фильтр по направлению/статусу.
    /// </summary>
    public partial class SearchViewModel : ViewModelBase
    {
        private readonly IDocumentRepository _documents;

        public ObservableCollection<Document> Results { get; } = new ObservableCollection<Document>();

        public DocumentDirection?[] AvailableDirections { get; } =
        {
            null,
            DocumentDirection.Incoming,
            DocumentDirection.Outgoing,
            DocumentDirection.Internal
        };

        public DocumentStatus?[] AvailableStatuses { get; } =
        {
            null,
            DocumentStatus.New,
            DocumentStatus.InProgress,
            DocumentStatus.OnHold,
            DocumentStatus.Completed,
            DocumentStatus.Cancelled
        };

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
        private string query;

        [ObservableProperty]
        private DocumentDirection? selectedDirection;

        [ObservableProperty]
        private DocumentStatus? selectedStatus;

        [ObservableProperty]
        private string statusMessage;

        [ObservableProperty]
        private string errorMessage;

        public SearchViewModel(IDocumentRepository documents)
        {
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
        }

        [RelayCommand(CanExecute = nameof(HasQuery))]
        private void Search()
        {
            ErrorMessage = null;
            Results.Clear();
            try
            {
                var filter = new DocumentSearchFilter
                {
                    Text = Query,
                    Direction = SelectedDirection,
                    Status = SelectedStatus
                };
                var found = _documents.Search(filter);
                foreach (var d in found) Results.Add(d);
                StatusMessage = $"Найдено: {Results.Count}";
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        [RelayCommand]
        private void Reset()
        {
            Query = null;
            SelectedDirection = null;
            SelectedStatus = null;
            Results.Clear();
            StatusMessage = null;
            ErrorMessage = null;
        }

        private bool HasQuery() => !string.IsNullOrWhiteSpace(Query);
    }
}
