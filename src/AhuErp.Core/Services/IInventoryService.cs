using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Бизнес-операции склада. Гарантирует неотрицательность остатка и запись
    /// движения (<see cref="InventoryTransaction"/>) как единую транзакцию.
    /// </summary>
    public interface IInventoryService
    {
        /// <summary>
        /// Зарегистрировать приход (<paramref name="quantityChange"/> &gt; 0) или
        /// списание (<paramref name="quantityChange"/> &lt; 0) позиции склада.
        /// </summary>
        /// <param name="itemId">Идентификатор номенклатурной позиции.</param>
        /// <param name="quantityChange">Положительное — приход, отрицательное — расход. Не может быть 0.</param>
        /// <param name="documentId">Документ-основание. Обязателен для списаний, опционален для приходов.</param>
        /// <param name="userId">Инициатор операции.</param>
        /// <returns>Сохранённая запись о движении.</returns>
        InventoryTransaction ProcessTransaction(int itemId, int quantityChange, int? documentId, int userId);
    }
}
