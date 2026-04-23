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
    /// ViewModel раздела «IT-служба» (Help Desk). Даёт CRUD для <see cref="ItTicket"/>
    /// и позволяет при закрытии заявки списать расходник со склада —
    /// списание проходит через <see cref="IInventoryService"/> и привязывается
    /// к <see cref="ItTicket.Id"/> как к документу-основанию.
    /// </summary>
    public partial class ItServiceViewModel : ViewModelBase
    {
        private readonly IDocumentRepository _documents;
        private readonly IInventoryRepository _inventory;
        private readonly IInventoryService _inventoryService;
        private readonly IAuthService _auth;

        public ObservableCollection<ItTicket> Tickets { get; }
        public ObservableCollection<InventoryItem> Items { get; }

        public DocumentStatus[] Statuses { get; } =
            (DocumentStatus[])Enum.GetValues(typeof(DocumentStatus));

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
        [NotifyCanExecuteChangedFor(nameof(ResolveCommand))]
        private ItTicket selectedTicket;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
        private string draftTitle;

        [ObservableProperty]
        private string draftAffectedEquipment;

        [ObservableProperty]
        private string draftResolutionNotes;

        [ObservableProperty]
        private DocumentStatus draftStatus = DocumentStatus.New;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ResolveCommand))]
        private InventoryItem consumedItem;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ResolveCommand))]
        private int consumedQuantity;

        [ObservableProperty]
        private string errorMessage;

        [ObservableProperty]
        private string statusMessage;

        public ItServiceViewModel(IDocumentRepository documents,
                                  IInventoryRepository inventory,
                                  IInventoryService inventoryService,
                                  IAuthService auth)
        {
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));

            Tickets = new ObservableCollection<ItTicket>();
            Items = new ObservableCollection<InventoryItem>();
            Reload();
        }

        partial void OnSelectedTicketChanged(ItTicket value)
        {
            ErrorMessage = null;
            StatusMessage = null;
            if (value == null)
            {
                DraftTitle = null;
                DraftAffectedEquipment = null;
                DraftResolutionNotes = null;
                DraftStatus = DocumentStatus.New;
                return;
            }
            DraftTitle = value.Title;
            DraftAffectedEquipment = value.AffectedEquipment;
            DraftResolutionNotes = value.ResolutionNotes;
            DraftStatus = value.Status;
        }

        [RelayCommand]
        private void New()
        {
            SelectedTicket = null;
            DraftTitle = string.Empty;
            DraftAffectedEquipment = string.Empty;
            DraftResolutionNotes = string.Empty;
            DraftStatus = DocumentStatus.New;
            ConsumedItem = null;
            ConsumedQuantity = 0;
            ErrorMessage = null;
            StatusMessage = null;
        }

        [RelayCommand(CanExecute = nameof(CanSave))]
        private void Save()
        {
            ErrorMessage = null;
            StatusMessage = null;
            try
            {
                if (SelectedTicket == null)
                {
                    var ticket = new ItTicket
                    {
                        Title = DraftTitle,
                        AffectedEquipment = DraftAffectedEquipment,
                        ResolutionNotes = DraftResolutionNotes,
                        CreationDate = DateTime.Now,
                        Deadline = DateTime.Now.AddDays(7),
                        Status = DraftStatus
                    };
                    _documents.Add(ticket);
                }
                else
                {
                    SelectedTicket.Title = DraftTitle;
                    SelectedTicket.AffectedEquipment = DraftAffectedEquipment;
                    SelectedTicket.ResolutionNotes = DraftResolutionNotes;
                    SelectedTicket.Status = DraftStatus;
                    _documents.Update(SelectedTicket);
                }
                Reload();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        [RelayCommand(CanExecute = nameof(HasSelection))]
        private void Delete()
        {
            if (SelectedTicket == null) return;
            _documents.Remove(SelectedTicket.Id);
            Reload();
            New();
        }

        [RelayCommand(CanExecute = nameof(CanResolve))]
        private void Resolve()
        {
            ErrorMessage = null;
            StatusMessage = null;
            try
            {
                var user = _auth.CurrentEmployee
                    ?? throw new InvalidOperationException("Пользователь не аутентифицирован.");

                if (ConsumedItem != null && ConsumedQuantity > 0)
                {
                    _inventoryService.ProcessTransaction(
                        itemId: ConsumedItem.Id,
                        quantityChange: -ConsumedQuantity,
                        documentId: SelectedTicket.Id,
                        userId: user.Id);
                }

                SelectedTicket.Status = DocumentStatus.Completed;
                SelectedTicket.ResolutionNotes = DraftResolutionNotes;
                _documents.Update(SelectedTicket);

                StatusMessage = ConsumedItem == null
                    ? $"Заявка #{SelectedTicket.Id} закрыта."
                    : $"Заявка #{SelectedTicket.Id} закрыта, списано {ConsumedQuantity} × «{ConsumedItem.Name}».";

                ConsumedItem = null;
                ConsumedQuantity = 0;
                Reload();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        private bool CanSave() => !string.IsNullOrWhiteSpace(DraftTitle);
        private bool HasSelection() => SelectedTicket != null;

        private bool CanResolve() =>
            SelectedTicket != null
            && (ConsumedItem == null || ConsumedQuantity > 0);

        private void Reload()
        {
            var ticketId = SelectedTicket?.Id;

            Tickets.Clear();
            foreach (var t in _documents.ListItTickets().OrderByDescending(t => t.CreationDate))
                Tickets.Add(t);

            Items.Clear();
            foreach (var i in _inventory.ListItems().OrderBy(i => i.Name))
                Items.Add(i);

            SelectedTicket = Tickets.FirstOrDefault(t => t.Id == ticketId);
        }
    }
}
