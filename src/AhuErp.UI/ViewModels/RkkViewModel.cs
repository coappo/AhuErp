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
    /// Регистрационно-контрольная карточка (РКК) — центральный экран СЭД.
    /// Шесть вкладок: реквизиты, вложения, поручения и контроль, согласование,
    /// связанные хозяйственные операции, история и аудит.
    /// </summary>
    public partial class RkkViewModel : ViewModelBase
    {
        private readonly IDocumentRepository _documents;
        private readonly INomenclatureService _nomenclature;
        private readonly IAttachmentService _attachments;
        private readonly ITaskService _tasksService;
        private readonly IApprovalService _approvals;
        private readonly IAuditService _audit;
        private readonly IAuthService _auth;

        public ObservableCollection<Document> Documents { get; }
            = new ObservableCollection<Document>();

        public ObservableCollection<DocumentTypeRef> DocumentTypes { get; }
            = new ObservableCollection<DocumentTypeRef>();

        public ObservableCollection<NomenclatureCase> NomenclatureCases { get; }
            = new ObservableCollection<NomenclatureCase>();

        public ObservableCollection<DocumentAttachment> Attachments { get; }
            = new ObservableCollection<DocumentAttachment>();

        public ObservableCollection<DocumentTask> Tasks { get; }
            = new ObservableCollection<DocumentTask>();

        public ObservableCollection<DocumentApproval> Approvals { get; }
            = new ObservableCollection<DocumentApproval>();

        public ObservableCollection<AuditLog> History { get; }
            = new ObservableCollection<AuditLog>();

        public DocumentDirection[] Directions { get; } =
            (DocumentDirection[])Enum.GetValues(typeof(DocumentDirection));

        public DocumentAccessLevel[] AccessLevels { get; } =
            (DocumentAccessLevel[])Enum.GetValues(typeof(DocumentAccessLevel));

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
        [NotifyCanExecuteChangedFor(nameof(AddTaskCommand))]
        private Document selectedDocument;

        [ObservableProperty]
        private DocumentTypeRef selectedType;

        [ObservableProperty]
        private NomenclatureCase selectedCase;

        [ObservableProperty]
        private DocumentDirection selectedDirection = DocumentDirection.Internal;

        [ObservableProperty]
        private DocumentAccessLevel selectedAccessLevel = DocumentAccessLevel.Internal;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        private string draftTitle;

        [ObservableProperty]
        private string draftSummary;

        [ObservableProperty]
        private string draftCorrespondent;

        [ObservableProperty]
        private DateTime draftDeadline = DateTime.Today.AddDays(7);

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddTaskCommand))]
        private string newTaskDescription;

        [ObservableProperty]
        private DateTime newTaskDeadline = DateTime.Today.AddDays(3);

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddTaskCommand))]
        private int newTaskExecutorId;

        [ObservableProperty]
        private string errorMessage;

        public RkkViewModel(
            IDocumentRepository documents,
            INomenclatureService nomenclature,
            IAttachmentService attachments,
            ITaskService tasks,
            IApprovalService approvals,
            IAuditService audit,
            IAuthService auth)
        {
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _nomenclature = nomenclature ?? throw new ArgumentNullException(nameof(nomenclature));
            _attachments = attachments ?? throw new ArgumentNullException(nameof(attachments));
            _tasksService = tasks ?? throw new ArgumentNullException(nameof(tasks));
            _approvals = approvals ?? throw new ArgumentNullException(nameof(approvals));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));

            Reload();
        }

        partial void OnSelectedDocumentChanged(Document value)
        {
            if (value == null)
            {
                ClearDraft();
                return;
            }
            DraftTitle = value.Title;
            DraftSummary = value.Summary;
            DraftCorrespondent = value.Correspondent;
            DraftDeadline = value.Deadline == default ? DateTime.Today.AddDays(7) : value.Deadline;
            SelectedDirection = value.Direction;
            SelectedAccessLevel = value.AccessLevel;
            SelectedType = DocumentTypes.FirstOrDefault(t => t.Id == value.DocumentTypeRefId);
            SelectedCase = NomenclatureCases.FirstOrDefault(c => c.Id == value.NomenclatureCaseId);
            ReloadAttachments();
            ReloadTasks();
            ReloadApprovals();
            ReloadHistory();
        }

        [RelayCommand]
        private void Reload()
        {
            ErrorMessage = null;
            DocumentTypes.Clear();
            foreach (var t in _nomenclature.ListTypes()) DocumentTypes.Add(t);
            NomenclatureCases.Clear();
            foreach (var c in _nomenclature.ListCases()) NomenclatureCases.Add(c);
            Documents.Clear();
            // Загружаем все «офисные» направления документов.
            foreach (var d in _documents.ListByType(DocumentType.Internal)
                                       .Concat(_documents.ListByType(DocumentType.Office))
                                       .Concat(_documents.ListByType(DocumentType.Incoming))
                                       .OrderByDescending(d => d.CreationDate))
            {
                Documents.Add(d);
            }
        }

        [RelayCommand]
        private void New()
        {
            SelectedDocument = null;
            ClearDraft();
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private void Save()
        {
            ErrorMessage = null;
            try
            {
                if (SelectedDocument == null)
                {
                    var doc = new Document
                    {
                        Title = DraftTitle,
                        Summary = DraftSummary,
                        Correspondent = DraftCorrespondent,
                        Type = MapDirectionToType(SelectedDirection),
                        Direction = SelectedDirection,
                        AccessLevel = SelectedAccessLevel,
                        CreationDate = DateTime.Now,
                        Deadline = DraftDeadline,
                        Status = DocumentStatus.New,
                        DocumentTypeRefId = SelectedType?.Id,
                        NomenclatureCaseId = SelectedCase?.Id,
                        AuthorId = _auth.CurrentEmployee?.Id
                    };
                    _documents.Add(doc);
                    _audit.Record(AuditActionType.Created, nameof(Document), doc.Id,
                        _auth.CurrentEmployee?.Id, newValues: $"Title={doc.Title}");
                    Reload();
                    SelectedDocument = Documents.FirstOrDefault(d => d.Id == doc.Id);
                }
                else
                {
                    var doc = SelectedDocument;
                    doc.Title = DraftTitle;
                    doc.Summary = DraftSummary;
                    doc.Correspondent = DraftCorrespondent;
                    doc.Direction = SelectedDirection;
                    doc.AccessLevel = SelectedAccessLevel;
                    doc.Deadline = DraftDeadline;
                    doc.DocumentTypeRefId = SelectedType?.Id;
                    doc.NomenclatureCaseId = SelectedCase?.Id;
                    _documents.Update(doc);
                    _audit.Record(AuditActionType.Updated, nameof(Document), doc.Id,
                        _auth.CurrentEmployee?.Id, newValues: $"Title={doc.Title}");
                    ReloadHistory();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        [RelayCommand(CanExecute = nameof(CanRegister))]
        private void Register()
        {
            ErrorMessage = null;
            try
            {
                // Запоминаем Id ДО Reload(): тот очищает Documents, что в свою
                // очередь сбрасывает SelectedDocument в null через WPF-биндинг.
                var docId = SelectedDocument.Id;
                _nomenclature.Register(docId, SelectedCase?.Id);
                Reload();
                SelectedDocument = Documents.FirstOrDefault(d => d.Id == docId);
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        [RelayCommand(CanExecute = nameof(CanAddTask))]
        private void AddTask()
        {
            ErrorMessage = null;
            try
            {
                var actor = _auth.CurrentEmployee?.Id ?? 0;
                _tasksService.CreateTask(
                    SelectedDocument.Id,
                    authorId: actor,
                    executorId: NewTaskExecutorId,
                    description: NewTaskDescription,
                    deadline: NewTaskDeadline.Date.AddDays(1).AddSeconds(-1));
                NewTaskDescription = null;
                ReloadTasks();
                ReloadHistory();
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        private static DocumentType MapDirectionToType(DocumentDirection dir)
        {
            switch (dir)
            {
                case DocumentDirection.Incoming: return DocumentType.Incoming;
                case DocumentDirection.Outgoing: return DocumentType.Office;
                case DocumentDirection.Directive: return DocumentType.Office;
                default: return DocumentType.Internal;
            }
        }

        private void ReloadAttachments()
        {
            Attachments.Clear();
            if (SelectedDocument == null) return;
            foreach (var a in _attachments.ListByDocument(SelectedDocument.Id)) Attachments.Add(a);
        }

        private void ReloadTasks()
        {
            Tasks.Clear();
            if (SelectedDocument == null) return;
            foreach (var t in _tasksService.ListByDocument(SelectedDocument.Id)) Tasks.Add(t);
        }

        private void ReloadApprovals()
        {
            Approvals.Clear();
            if (SelectedDocument == null) return;
            foreach (var a in _approvals.ListByDocument(SelectedDocument.Id)) Approvals.Add(a);
        }

        private void ReloadHistory()
        {
            History.Clear();
            if (SelectedDocument == null) return;
            var entries = _audit.Query(new AuditQueryFilter
            {
                EntityType = nameof(Document),
                EntityId = SelectedDocument.Id,
                Top = 200
            });
            foreach (var e in entries) History.Add(e);
        }

        private void ClearDraft()
        {
            DraftTitle = null;
            DraftSummary = null;
            DraftCorrespondent = null;
            DraftDeadline = DateTime.Today.AddDays(7);
            SelectedDirection = DocumentDirection.Internal;
            SelectedAccessLevel = DocumentAccessLevel.Internal;
            SelectedType = null;
            SelectedCase = null;
            Attachments.Clear();
            Tasks.Clear();
            Approvals.Clear();
            History.Clear();
        }

        private bool CanSave() => !string.IsNullOrWhiteSpace(DraftTitle);
        private bool CanRegister() => SelectedDocument != null && !SelectedDocument.IsRegistered;
        private bool CanAddTask() => SelectedDocument != null
            && !string.IsNullOrWhiteSpace(NewTaskDescription)
            && NewTaskExecutorId > 0;
    }
}
