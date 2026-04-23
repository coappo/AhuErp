namespace AhuErp.Core.Models
{
    /// <summary>
    /// Категория ТМЦ. Используется для фильтрации и отчётности по складу.
    /// Значения хранятся как int — добавление новых членов в конец не требует миграции EF6.
    /// </summary>
    public enum InventoryCategory
    {
        Stationery = 0,
        IT_Equipment = 1,
        Cleaning_Supplies = 2
    }
}
