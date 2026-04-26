using System;

namespace AhuErp.Core.Reports
{
    /// <summary>
    /// DTO-строки для регламентированных отчётов Phase 12.
    /// Это POCO-«рекорды» — только данные, без логики и без зависимости
    /// на ClosedXML / OpenXml / PdfSharp.
    /// </summary>
    public sealed class DispatchRow
    {
        public string RegistrationNumber { get; set; }
        public DateTime? RegistrationDate { get; set; }
        public string Title { get; set; }
        public string Correspondent { get; set; }
        public string Recipient { get; set; }
        public string DispatchMethod { get; set; }
    }

    public sealed class CaseInventoryRow
    {
        public int Index { get; set; }
        public string RegistrationNumber { get; set; }
        public DateTime? RegistrationDate { get; set; }
        public string Title { get; set; }
        public int PageCount { get; set; }
        public string Notes { get; set; }
    }

    public sealed class FleetTripRow
    {
        public int VehicleId { get; set; }
        public string VehicleModel { get; set; }
        public string LicensePlate { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string DriverName { get; set; }
        public string Purpose { get; set; }
        public TimeSpan Duration => EndDate - StartDate;
    }

    public sealed class TurnoverRow
    {
        public int InventoryItemId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public int OpeningBalance { get; set; }
        public int Incoming { get; set; }
        public int Outgoing { get; set; }
        public int ClosingBalance { get; set; }
    }

    public sealed class AuditTrailRow
    {
        public DateTime Timestamp { get; set; }
        public string Action { get; set; }
        public string EntityType { get; set; }
        public int? EntityId { get; set; }
        public int? UserId { get; set; }
        public string Details { get; set; }
        public string Hash { get; set; }
    }
}
