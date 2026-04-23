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
    /// ViewModel раздела «Склад / ТМЦ». Реализует приход и расход позиций
    /// через <see cref="IInventoryService"/>; расход обязательно привязывается
    /// к документу-основанию (внутренний приказ или IT-заявка).
    /// </summary>
    public partial class WarehouseViewModel : ViewModelBase
    {
        private readonly IInventoryRepository _inventory;
        private readonly IInventoryService _inventoryService;
        private readonly IDocumentRepository _documents;
        private readonly IAuthService _auth;

        public ObservableCollection<InventoryItem> Items { get; }

        public ObservableCollection<Document> EligibleDocuments { get; }

        public ObservableCollection<InventoryTransaction> RecentTransactions { get; }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DeductCommand))]
        [NotifyCanExecuteChangedFor(nameof(RestockCommand))]
        private InventoryItem selectedItem;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DeductCommand))]
        [NotifyCanExecuteChangedFor(nameof(RestockCommand))]
        private int quantity = 1;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(DeductCommand))]
        private Document selectedDocument;

        [ObservableProperty]
        private string errorMessage;

        [ObservableProperty]
        private string statusMessage;

        public WarehouseViewModel(IInventoryRepository inventory,
                                  IInventoryService inventoryService,
                                  IDocumentRepository documents,
                                  IAuthService auth)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _inventoryService = inventoryService ?? throw new ArgumentNullException(nameof(inventoryService));
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));

            Items = new ObservableCollection<InventoryItem>();
            EligibleDocuments = new ObservableCollection<Document>();
            RecentTransactions = new ObservableCollection<InventoryTransaction>();
            Reload();
        }

        [RelayCommand(CanExecute = nameof(CanDeduct))]
        private void Deduct()
        {
            Apply(-Math.Abs(Quantity), requireDocument: true,
                  successText: $"Списано {Quantity} × «{SelectedItem.Name}».");
        }

        [RelayCommand(CanExecute = nameof(CanRestock))]
        private void Restock()
        {
            Apply(Math.Abs(Quantity), requireDocument: false,
                  successText: $"Приход {Quantity} × «{SelectedItem.Name}».");
        }

        [RelayCommand]
        private void Refresh() => Reload();

        private void Apply(int change, bool requireDocument, string successText)
        {
            ErrorMessage = null;
            StatusMessage = null;
            try
            {
                var user = _auth.CurrentEmployee
                    ?? throw new InvalidOperationException("Пользователь не аутентифицирован.");
                int? docId = requireDocument ? SelectedDocument?.Id : SelectedDocument?.Id;
                _inventoryService.ProcessTransaction(SelectedItem.Id, change, docId, user.Id);
                StatusMessage = successText;
                Reload();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
        }

        private bool CanDeduct() =>
            SelectedItem != null
            && Quantity > 0
            && SelectedDocument != null;

        private bool CanRestock() =>
            SelectedItem != null && Quantity > 0;

        private void Reload()
        {
            var itemId = SelectedItem?.Id;
            var docId = SelectedDocument?.Id;

            Items.Clear();
            foreach (var it in _inventory.ListItems().OrderBy(i => i.Name))
                Items.Add(it);

            EligibleDocuments.Clear();
            foreach (var doc in _documents.ListInventoryEligibleDocuments()
                                          .OrderByDescending(d => d.CreationDate))
                EligibleDocuments.Add(doc);

            RecentTransactions.Clear();
            foreach (var tx in _inventory.ListTransactions().Take(20))
                RecentTransactions.Add(tx);

            SelectedItem = Items.FirstOrDefault(i => i.Id == itemId) ?? Items.FirstOrDefault();
            SelectedDocument = EligibleDocuments.FirstOrDefault(d => d.Id == docId);
        }
    }
}
