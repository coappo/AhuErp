using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Абстракция хранилища ТМЦ. В Phase 3 используется in-memory реализация
    /// из UI-слоя; на боевом окружении подменяется реализацией поверх
    /// <see cref="Data.AhuDbContext"/>.
    /// </summary>
    public interface IInventoryRepository
    {
        IReadOnlyList<InventoryItem> ListItems();

        InventoryItem GetItem(int itemId);

        IReadOnlyList<InventoryTransaction> ListTransactions(int? itemId = null);

        void AddItem(InventoryItem item);

        void RecordTransaction(InventoryTransaction transaction);
    }
}
