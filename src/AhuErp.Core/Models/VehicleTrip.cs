using System;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Путевой лист — бронирование транспортного средства на интервал времени.
    /// В Phase 4 обязательно привязан к документу-основанию (заявка на транспорт)
    /// и содержит ФИО водителя.
    /// </summary>
    public class VehicleTrip
    {
        public int Id { get; set; }

        public int VehicleId { get; set; }

        public virtual Vehicle Vehicle { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        /// <summary>
        /// Документ-основание (заявка на транспорт). Nullable на уровне БД
        /// для обратной совместимости с ранее созданными поездками; новый API
        /// бронирования в Phase 4 требует заполненного значения.
        /// </summary>
        public int? DocumentId { get; set; }

        public virtual Document Document { get; set; }

        [StringLength(128)]
        public string DriverName { get; set; }

        /// <summary>
        /// Интервалы пересекаются при выполнении условия Allen-overlap: start1 &lt; end2 и start2 &lt; end1.
        /// </summary>
        public bool OverlapsWith(DateTime otherStart, DateTime otherEnd)
        {
            return StartDate < otherEnd && otherStart < EndDate;
        }
    }
}
