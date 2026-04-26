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
        private readonly IInventoryService _inventory;
        private readonly IInventoryRepository _inventoryRepo;
        private readonly IFleetService _fleet;
        private readonly IVehicleRepository _vehicleRepo;
        private readonly ISignatureService _signatures;

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

        public ObservableCollection<DocumentSignature> Signatures { get; }
            = new ObservableCollection<DocumentSignature>();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SignSimpleCommand))]
        [NotifyCanExecuteChangedFor(nameof(SignQualifiedCommand))]
        [NotifyCanExecuteChangedFor(nameof(RevokeSignatureCommand))]
        private DocumentSignature selectedSignature;

        [ObservableProperty]
        private string signReason;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SignQualifiedCommand))]
        private string signCertificateThumbprint;

        public ObservableCollection<InventoryTransaction> RelatedInventoryTx { get; }
            = new ObservableCollection<InventoryTransaction>();

        public ObservableCollection<VehicleTrip> RelatedTrips { get; }
            = new ObservableCollection<VehicleTrip>();

        public ObservableCollection<InventoryItem> InventoryItems { get; }
            = new ObservableCollection<InventoryItem>();

        public ObservableCollection<Vehicle> Vehicles { get; }
            = new ObservableCollection<Vehicle>();

        // Поля диалога связанной операции «Списание ТМЦ»
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CreateInventoryWriteOffCommand))]
        private InventoryItem newWriteOffItem;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CreateInventoryWriteOffCommand))]
        private int newWriteOffQuantity = 1;

        // Поля диалога связанной операции «Путевой лист»
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CreateVehicleTripCommand))]
        private Vehicle newTripVehicle;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CreateVehicleTripCommand))]
        private DateTime newTripStart = DateTime.Today;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CreateVehicleTripCommand))]
        private DateTime newTripEnd = DateTime.Today.AddDays(1);

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CreateVehicleTripCommand))]
        private string newTripDriver;

        public DocumentDirection[] Directions { get; } =
            (DocumentDirection[])Enum.GetValues(typeof(DocumentDirection));

        public DocumentAccessLevel[] AccessLevels { get; } =
            (DocumentAccessLevel[])Enum.GetValues(typeof(DocumentAccessLevel));

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        [NotifyCanExecuteChangedFor(nameof(RegisterCommand))]
        [NotifyCanExecuteChangedFor(nameof(AddTaskCommand))]
        [NotifyCanExecuteChangedFor(nameof(CreateInventoryWriteOffCommand))]
        [NotifyCanExecuteChangedFor(nameof(CreateVehicleTripCommand))]
        [NotifyCanExecuteChangedFor(nameof(CreateArchiveRequestCommand))]
        [NotifyCanExecuteChangedFor(nameof(CreateItTicketCommand))]
        [NotifyCanExecuteChangedFor(nameof(SignSimpleCommand))]
        [NotifyCanExecuteChangedFor(nameof(SignQualifiedCommand))]
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
            IAuthService auth,
            IInventoryService inventory,
            IInventoryRepository inventoryRepo,
            IFleetService fleet,
            IVehicleRepository vehicleRepo,
            ISignatureService signatures = null)
        {
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _nomenclature = nomenclature ?? throw new ArgumentNullException(nameof(nomenclature));
            _attachments = attachments ?? throw new ArgumentNullException(nameof(attachments));
            _tasksService = tasks ?? throw new ArgumentNullException(nameof(tasks));
            _approvals = approvals ?? throw new ArgumentNullException(nameof(approvals));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _inventoryRepo = inventoryRepo ?? throw new ArgumentNullException(nameof(inventoryRepo));
            _fleet = fleet ?? throw new ArgumentNullException(nameof(fleet));
            _vehicleRepo = vehicleRepo ?? throw new ArgumentNullException(nameof(vehicleRepo));
            _signatures = signatures;

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
            ReloadRelatedOps();
            ReloadSignatures();
        }

        [RelayCommand]
        private void Reload()
        {
            ErrorMessage = null;
            DocumentTypes.Clear();
            foreach (var t in _nomenclature.ListTypes()) DocumentTypes.Add(t);
            NomenclatureCases.Clear();
            foreach (var c in _nomenclature.ListCases()) NomenclatureCases.Add(c);
            InventoryItems.Clear();
            foreach (var i in _inventoryRepo.ListItems().OrderBy(i => i.Name))
                InventoryItems.Add(i);
            Vehicles.Clear();
            foreach (var v in _vehicleRepo.ListVehicles().OrderBy(v => v.LicensePlate))
                Vehicles.Add(v);
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

        // ── Task 7: «Создать связанную операцию» ──────────────────────────

        [RelayCommand(CanExecute = nameof(CanWriteOff))]
        private void CreateInventoryWriteOff()
        {
            ErrorMessage = null;
            try
            {
                var actor = _auth.CurrentEmployee?.Id ?? 0;
                if (actor <= 0) throw new InvalidOperationException("Не определён текущий пользователь.");
                if (NewWriteOffQuantity <= 0)
                    throw new InvalidOperationException("Количество должно быть положительным.");

                var tx = _inventory.ProcessTransaction(
                    NewWriteOffItem.Id,
                    -NewWriteOffQuantity,
                    SelectedDocument.Id,
                    actor);

                _audit.Record(AuditActionType.Created, nameof(InventoryTransaction), tx.Id, actor,
                    newValues: $"BasisDocumentId={SelectedDocument.Id};Item={NewWriteOffItem.Name};Qty=-{NewWriteOffQuantity}");

                NewWriteOffItem = null;
                NewWriteOffQuantity = 1;
                ReloadRelatedOps();
                ReloadHistory();
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        [RelayCommand(CanExecute = nameof(CanCreateTrip))]
        private void CreateVehicleTrip()
        {
            ErrorMessage = null;
            try
            {
                var actor = _auth.CurrentEmployee?.Id ?? 0;
                if (actor <= 0) throw new InvalidOperationException("Не определён текущий пользователь.");

                var trip = _fleet.BookVehicle(
                    NewTripVehicle.Id,
                    SelectedDocument.Id,
                    NewTripStart,
                    NewTripEnd,
                    string.IsNullOrWhiteSpace(NewTripDriver) ? "—" : NewTripDriver);

                _audit.Record(AuditActionType.Created, nameof(VehicleTrip), trip.Id, actor,
                    newValues: $"BasisDocumentId={SelectedDocument.Id};Vehicle={NewTripVehicle.LicensePlate};Driver={NewTripDriver};{NewTripStart:yyyy-MM-dd}—{NewTripEnd:yyyy-MM-dd}");

                NewTripVehicle = null;
                NewTripDriver = null;
                NewTripStart = DateTime.Today;
                NewTripEnd = DateTime.Today.AddDays(1);
                ReloadRelatedOps();
                ReloadHistory();
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        [RelayCommand(CanExecute = nameof(HasSelectedDocument))]
        private void CreateArchiveRequest()
        {
            ErrorMessage = null;
            try
            {
                var actor = _auth.CurrentEmployee?.Id ?? 0;
                var req = new ArchiveRequest
                {
                    Title = $"Заявка на основании документа {SelectedDocument.RegistrationNumber ?? "#" + SelectedDocument.Id}",
                    Summary = SelectedDocument.Summary,
                    Type = DocumentType.Archive,
                    Direction = DocumentDirection.Internal,
                    AccessLevel = SelectedDocument.AccessLevel,
                    CreationDate = DateTime.Now,
                    Deadline = DateTime.Today.AddDays(30),
                    Status = DocumentStatus.New,
                    AuthorId = actor > 0 ? (int?)actor : null,
                    BasisDocumentId = SelectedDocument.Id
                };
                _documents.Add(req);
                _audit.Record(AuditActionType.Created, nameof(ArchiveRequest), req.Id, actor,
                    newValues: $"BasisDocumentId={SelectedDocument.Id}");
                ReloadHistory();
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        [RelayCommand(CanExecute = nameof(HasSelectedDocument))]
        private void CreateItTicket()
        {
            ErrorMessage = null;
            try
            {
                var actor = _auth.CurrentEmployee?.Id ?? 0;
                var ticket = new ItTicket
                {
                    Title = $"Заявка ИТ по документу {SelectedDocument.RegistrationNumber ?? "#" + SelectedDocument.Id}",
                    Summary = SelectedDocument.Summary,
                    Type = DocumentType.It,
                    Direction = DocumentDirection.Internal,
                    AccessLevel = SelectedDocument.AccessLevel,
                    CreationDate = DateTime.Now,
                    Deadline = DateTime.Today.AddDays(7),
                    Status = DocumentStatus.New,
                    AuthorId = actor > 0 ? (int?)actor : null,
                    BasisDocumentId = SelectedDocument.Id
                };
                _documents.Add(ticket);
                _audit.Record(AuditActionType.Created, nameof(ItTicket), ticket.Id, actor,
                    newValues: $"BasisDocumentId={SelectedDocument.Id}");
                ReloadHistory();
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        private bool HasSelectedDocument() => SelectedDocument != null;

        private bool CanWriteOff() =>
            SelectedDocument != null && NewWriteOffItem != null && NewWriteOffQuantity > 0;

        private bool CanCreateTrip() =>
            SelectedDocument != null && NewTripVehicle != null
            && NewTripEnd > NewTripStart;

        private void ReloadRelatedOps()
        {
            RelatedInventoryTx.Clear();
            RelatedTrips.Clear();
            if (SelectedDocument == null) return;

            foreach (var tx in _inventoryRepo.ListTransactions()
                                              .Where(t => t.BasisDocumentId == SelectedDocument.Id
                                                          || t.DocumentId == SelectedDocument.Id)
                                              .OrderByDescending(t => t.TransactionDate))
                RelatedInventoryTx.Add(tx);

            foreach (var v in _vehicleRepo.ListVehicles())
                foreach (var trip in _vehicleRepo.ListTrips(v.Id)
                                                  .Where(t => t.BasisDocumentId == SelectedDocument.Id
                                                              || t.DocumentId == SelectedDocument.Id))
                    RelatedTrips.Add(trip);
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
            Signatures.Clear();
        }

        // ---------------- Phase 8 — подписи -------------------------------

        private void ReloadSignatures()
        {
            Signatures.Clear();
            if (SelectedDocument == null || _signatures == null) return;
            foreach (var s in _signatures.ListByDocument(SelectedDocument.Id))
                Signatures.Add(s);
        }

        [RelayCommand(CanExecute = nameof(CanSign))]
        private void SignSimple()
        {
            ErrorMessage = null;
            try
            {
                var actor = _auth.CurrentEmployee?.Id ?? 0;
                if (actor == 0) { ErrorMessage = "Не определён текущий сотрудник."; return; }
                _signatures.Sign(SelectedDocument.Id, attachmentId: null, signerId: actor,
                    kind: SignatureKind.Simple, reason: SignReason);
                SignReason = null;
                ReloadSignatures();
                ReloadHistory();
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        [RelayCommand(CanExecute = nameof(CanSignQualified))]
        private void SignQualified()
        {
            ErrorMessage = null;
            try
            {
                var actor = _auth.CurrentEmployee?.Id ?? 0;
                if (actor == 0) { ErrorMessage = "Не определён текущий сотрудник."; return; }
                _signatures.Sign(SelectedDocument.Id, attachmentId: null, signerId: actor,
                    kind: SignatureKind.Qualified, reason: SignReason,
                    certificateThumbprint: SignCertificateThumbprint);
                SignReason = null;
                SignCertificateThumbprint = null;
                ReloadSignatures();
                ReloadHistory();
                // SelectedDocument теперь IsLocked=true — обновляем из репозитория.
                if (SelectedDocument != null)
                {
                    var refreshed = _documents.GetById(SelectedDocument.Id);
                    if (refreshed != null) SelectedDocument = refreshed;
                }
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        [RelayCommand(CanExecute = nameof(CanRevokeSignature))]
        private void RevokeSignature()
        {
            ErrorMessage = null;
            try
            {
                var actor = _auth.CurrentEmployee?.Id ?? 0;
                _signatures.Revoke(SelectedSignature.Id, actor,
                    SignReason ?? "Отзыв из РКК");
                SignReason = null;
                ReloadSignatures();
                ReloadHistory();
            }
            catch (Exception ex) { ErrorMessage = ex.Message; }
        }

        private bool CanSign() => SelectedDocument != null && _signatures != null;
        private bool CanSignQualified() => CanSign()
            && !string.IsNullOrWhiteSpace(SignCertificateThumbprint);
        private bool CanRevokeSignature() => SelectedSignature != null
            && !SelectedSignature.IsRevoked && _signatures != null;

        private bool CanSave() => !string.IsNullOrWhiteSpace(DraftTitle);
        private bool CanRegister() => SelectedDocument != null && !SelectedDocument.IsRegistered;
        private bool CanAddTask() => SelectedDocument != null
            && !string.IsNullOrWhiteSpace(NewTaskDescription)
            && NewTaskExecutorId > 0;
    }
}
