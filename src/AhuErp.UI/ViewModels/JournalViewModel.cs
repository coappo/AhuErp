using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using AhuErp.UI.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AhuErp.UI.ViewModels
{
    /// <summary>
    /// Журналы регистрации документов (входящие/исходящие/внутренние/по делу
    /// номенклатуры). Поддерживает период, корреспондент, статус, исполнитель,
    /// текстовый поиск и экспорт в Excel.
    /// </summary>
    public partial class JournalViewModel : ViewModelBase
    {
        private readonly IDocumentRepository _documents;
        private readonly INomenclatureRepository _nomenclature;
        private readonly IReportService _reports;
        private readonly IFileDialogService _fileDialog;

        public JournalKind[] AvailableKinds { get; } =
            (JournalKind[])Enum.GetValues(typeof(JournalKind));

        public ObservableCollection<NomenclatureCase> Cases { get; } =
            new ObservableCollection<NomenclatureCase>();

        public ObservableCollection<Document> Items { get; } =
            new ObservableCollection<Document>();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
        private JournalKind selectedKind = JournalKind.Incoming;

        [ObservableProperty]
        private NomenclatureCase selectedCase;

        [ObservableProperty]
        private DateTime fromDate = new DateTime(DateTime.Today.Year, 1, 1);

        [ObservableProperty]
        private DateTime toDate = DateTime.Today;

        [ObservableProperty]
        private string textFilter;

        [ObservableProperty]
        private string correspondentFilter;

        [ObservableProperty]
        private bool overdueOnly;

        [ObservableProperty]
        private string statusMessage;

        [ObservableProperty]
        private string errorMessage;

        public JournalViewModel(
            IDocumentRepository documents,
            INomenclatureRepository nomenclature,
            IReportService reports,
            IFileDialogService fileDialog)
        {
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _nomenclature = nomenclature ?? throw new ArgumentNullException(nameof(nomenclature));
            _reports = reports ?? throw new ArgumentNullException(nameof(reports));
            _fileDialog = fileDialog ?? throw new ArgumentNullException(nameof(fileDialog));
            ReloadCases();
            Reload();
        }

        partial void OnSelectedKindChanged(JournalKind value)
        {
            // При смене типа сбрасываем выбор дела, чтобы не оставлять
            // неприменимый фильтр (например, дело Архива при выборе ИСХ).
            if (value != JournalKind.ByCase) SelectedCase = null;
            Reload();
        }

        partial void OnSelectedCaseChanged(NomenclatureCase value) => Reload();
        partial void OnFromDateChanged(DateTime value) => Reload();
        partial void OnToDateChanged(DateTime value) => Reload();
        partial void OnOverdueOnlyChanged(bool value) => Reload();

        [RelayCommand]
        private void ApplyFilter() => Reload();

        [RelayCommand]
        private void Reload()
        {
            ErrorMessage = null;
            Items.Clear();
            try
            {
                var filter = BuildFilter();
                var list = _documents.Search(filter);
                foreach (var d in list) Items.Add(d);
                StatusMessage = $"Найдено: {Items.Count}";
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        [RelayCommand(CanExecute = nameof(CanExport))]
        private void Export()
        {
            ErrorMessage = null;
            try
            {
                var path = _fileDialog.PromptSaveFile(
                    "Сохранить журнал регистрации",
                    "Excel|*.xlsx",
                    DefaultFileName());
                if (string.IsNullOrEmpty(path)) return;

                _reports.ExportRegistrationJournal(Items.ToList(), GetTitle(), path);
                StatusMessage = $"Сохранено: {path}";
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        private bool CanExport() => Items.Count > 0;

        private void ReloadCases()
        {
            Cases.Clear();
            Cases.Add(null); // вариант «не фильтровать»
            foreach (var c in _nomenclature.ListCases(year: null, activeOnly: false)
                                            .OrderBy(c => c.Index))
                Cases.Add(c);
        }

        private DocumentSearchFilter BuildFilter()
        {
            var f = new DocumentSearchFilter
            {
                From = FromDate,
                To = ToDate.AddDays(1).AddSeconds(-1),
                RegisteredOnly = SelectedKind != JournalKind.All,
                Text = TextFilter,
                Correspondent = CorrespondentFilter,
                OverdueOnly = OverdueOnly
            };

            switch (SelectedKind)
            {
                case JournalKind.Incoming:
                    f.Direction = DocumentDirection.Incoming;
                    break;
                case JournalKind.Outgoing:
                    f.Direction = DocumentDirection.Outgoing;
                    break;
                case JournalKind.Internal:
                    f.Direction = DocumentDirection.Internal;
                    break;
                case JournalKind.ByCase:
                    if (SelectedCase != null) f.NomenclatureCaseId = SelectedCase.Id;
                    break;
                case JournalKind.All:
                    break;
            }
            return f;
        }

        private string GetTitle()
        {
            switch (SelectedKind)
            {
                case JournalKind.Incoming: return "Журнал входящих документов";
                case JournalKind.Outgoing: return "Журнал исходящих документов";
                case JournalKind.Internal: return "Журнал внутренних документов";
                case JournalKind.ByCase:
                    return SelectedCase == null
                        ? "Журнал по номенклатуре дел"
                        : $"Дело {SelectedCase.Index} — {SelectedCase.Title}";
                default: return "Журнал документов";
            }
        }

        private string DefaultFileName()
        {
            var prefix = "Journal";
            switch (SelectedKind)
            {
                case JournalKind.Incoming: prefix = "Vkhodyaschie"; break;
                case JournalKind.Outgoing: prefix = "Iskhodyaschie"; break;
                case JournalKind.Internal: prefix = "Vnutrennie"; break;
                case JournalKind.ByCase: prefix = "ByCase"; break;
            }
            return $"{prefix}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        }
    }

    /// <summary>Тип отображаемого журнала регистрации.</summary>
    public enum JournalKind
    {
        Incoming,
        Outgoing,
        Internal,
        ByCase,
        All
    }
}
