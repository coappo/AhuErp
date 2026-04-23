using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// In-memory реализация <see cref="IInventoryRepository"/> для Phase 3.
    /// При переходе на EF6 заменяется реализацией поверх <see cref="Data.AhuDbContext"/>
    /// без изменений в <see cref="InventoryService"/> и UI.
    /// </summary>
    public sealed class InMemoryInventoryRepository : IInventoryRepository
    {
        private readonly List<InventoryItem> _items = new List<InventoryItem>();
        private readonly List<InventoryTransaction> _transactions = new List<InventoryTransaction>();
        private int _nextItemId = 1;
        private int _nextTxId = 1;

        public IReadOnlyList<InventoryItem> ListItems() => _items.ToList();

        public InventoryItem GetItem(int itemId) =>
            _items.FirstOrDefault(i => i.Id == itemId);

        public IReadOnlyList<InventoryTransaction> ListTransactions(int? itemId = null) =>
            (itemId == null
                ? _transactions
                : _transactions.Where(t => t.InventoryItemId == itemId.Value))
            .OrderByDescending(t => t.TransactionDate)
            .ToList();

        public void AddItem(InventoryItem item)
        {
            if (item.Id == 0) item.Id = _nextItemId++;
            else _nextItemId = System.Math.Max(_nextItemId, item.Id + 1);
            _items.Add(item);
        }

        public void RecordTransaction(InventoryTransaction transaction)
        {
            if (transaction.Id == 0) transaction.Id = _nextTxId++;
            else _nextTxId = System.Math.Max(_nextTxId, transaction.Id + 1);
            _transactions.Add(transaction);
        }
    }
}
