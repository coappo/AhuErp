using System;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Движение по складу: приход (положительный <see cref="QuantityChanged"/>)
    /// или расход (отрицательный). Привязка к <see cref="Document"/> обязательна
    /// для управленческого учёта — каждое списание мотивировано распорядительным
    /// документом либо IT-заявкой (через тот же Document-ID).
    /// </summary>
    public class InventoryTransaction
    {
        public int Id { get; set; }

        public int InventoryItemId { get; set; }

        public virtual InventoryItem InventoryItem { get; set; }

        /// <summary>
        /// Документ-основание (распоряжение, IT-заявка). Nullable, т.к. первичная
        /// регистрация остатков может быть без документа.
        /// </summary>
        public int? DocumentId { get; set; }

        public virtual Document Document { get; set; }

        /// <summary>Положительное — приход, отрицательное — списание.</summary>
        public int QuantityChanged { get; set; }

        public DateTime TransactionDate { get; set; }

        /// <summary>Сотрудник, инициировавший операцию.</summary>
        public int InitiatorId { get; set; }

        public virtual Employee Initiator { get; set; }
    }
}
