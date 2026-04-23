using System;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Путевой лист — бронирование транспортного средства на интервал времени
    /// (опционально привязано к документу-основанию).
    /// </summary>
    public class VehicleTrip
    {
        public int Id { get; set; }

        public int VehicleId { get; set; }

        public virtual Vehicle Vehicle { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public int? DocumentId { get; set; }

        public virtual Document Document { get; set; }

        /// <summary>
        /// Интервалы пересекаются при выполнении условия Allen-overlap: start1 &lt; end2 и start2 &lt; end1.
        /// </summary>
        public bool OverlapsWith(DateTime otherStart, DateTime otherEnd)
        {
            return StartDate < otherEnd && otherStart < EndDate;
        }
    }
}
