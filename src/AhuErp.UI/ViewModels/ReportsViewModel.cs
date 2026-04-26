using System;
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
    /// Phase 12 — единая точка генерации регламентированных отчётов СЭД:
    /// реестр исходящих, опись дела, отчёты по парку/складу/дисциплине,
    /// PDF-выгрузка истории документа.
    /// </summary>
    public partial class ReportsViewModel : ViewModelBase
    {
        private readonly IReportService _reports;
        private readonly IFileDialogService _fileDialog;
        private readonly INomenclatureRepository _nomenclature;
        private readonly IDocumentRepository _documents;

        public ObservableCollection<NomenclatureCase> Cases { get; } =
            new ObservableCollection<NomenclatureCase>();

        public ObservableCollection<Document> Documents { get; } =
            new ObservableCollection<Document>();

        [ObservableProperty]
        private DateTime fromDate = new DateTime(DateTime.Today.Year, 1, 1);

        [ObservableProperty]
        private DateTime toDate = DateTime.Today;

        [ObservableProperty]
        private NomenclatureCase selectedCase;

        [ObservableProperty]
        private Document selectedDocument;

        [ObservableProperty]
        private string statusMessage;

        [ObservableProperty]
        private string errorMessage;

        public ReportsViewModel(
            IReportService reports,
            IFileDialogService fileDialog,
            INomenclatureRepository nomenclature,
            IDocumentRepository documents)
        {
            _reports = reports ?? throw new ArgumentNullException(nameof(reports));
            _fileDialog = fileDialog ?? throw new ArgumentNullException(nameof(fileDialog));
            _nomenclature = nomenclature ?? throw new ArgumentNullException(nameof(nomenclature));
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));

            foreach (var c in _nomenclature.ListCases(year: null, activeOnly: true)
                                            .OrderBy(c => c.Index))
                Cases.Add(c);
            foreach (var d in _documents.Search(new DocumentSearchFilter { RegisteredOnly = true })
                                         .OrderByDescending(d => d.RegistrationDate))
                Documents.Add(d);
        }

        [RelayCommand]
        private void ExportOutgoingDispatch() => Run("Реестр_исходящих", "xlsx",
            path => _reports.ExportOutgoingDispatchRegistry(FromDate, ToDate, path));

        [RelayCommand(CanExecute = nameof(CanExportCaseInventory))]
        private void ExportCaseInventory() => Run($"Опись_дела_{SelectedCase?.Index}", "docx",
            path => _reports.GenerateCaseInventory(SelectedCase.Id, path));

        [RelayCommand]
        private void ExportFleet() => Run("Отчёт_парк", "xlsx",
            path => _reports.ExportFleetReport(FromDate, ToDate, path));

        [RelayCommand]
        private void ExportInventoryTurnover() => Run("Оборот_склада", "xlsx",
            path => _reports.ExportInventoryTurnoverReport(FromDate, ToDate, path));

        [RelayCommand(CanExecute = nameof(CanExportAuditTrail))]
        private void ExportAuditTrail() => Run($"Аудит_{SelectedDocument?.RegistrationNumber}", "pdf",
            path => _reports.ExportDocumentAuditTrail(SelectedDocument.Id, path));

        private bool CanExportCaseInventory() => SelectedCase != null;
        private bool CanExportAuditTrail() => SelectedDocument != null;

        partial void OnSelectedCaseChanged(NomenclatureCase value) =>
            ExportCaseInventoryCommand.NotifyCanExecuteChanged();

        partial void OnSelectedDocumentChanged(Document value) =>
            ExportAuditTrailCommand.NotifyCanExecuteChanged();

        private void Run(string defaultName, string ext, Action<string> action)
        {
            ErrorMessage = null;
            try
            {
                var path = _fileDialog.PromptSaveFile(
                    "Сохранить отчёт",
                    $"{ext.ToUpperInvariant()}|*.{ext}",
                    $"{defaultName}.{ext}");
                if (string.IsNullOrEmpty(path)) return;
                action(path);
                StatusMessage = $"Сохранено: {path}";
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }
    }
}
