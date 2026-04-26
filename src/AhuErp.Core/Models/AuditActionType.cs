namespace AhuErp.Core.Models
{
    /// <summary>
    /// Категория действия в журнале аудита. Список расширяемый — добавление
    /// новых членов в конец не требует миграции (значение хранится как int).
    /// </summary>
    public enum AuditActionType
    {
        Created = 0,
        Updated = 1,
        Deleted = 2,

        StatusChanged = 10,
        Registered = 11,
        AssignedToCase = 12,

        AttachmentAdded = 20,
        AttachmentVersioned = 21,
        AttachmentRemoved = 22,
        AttachmentViewed = 23,

        ResolutionIssued = 30,
        TaskAssigned = 31,
        TaskCompleted = 32,
        TaskOverdue = 33,
        TaskReassigned = 34,

        ApprovalSent = 40,
        ApprovalSigned = 41,
        ApprovalRejected = 42,

        InventoryTransactionRecorded = 50,
        VehicleTripBooked = 51,
        ArchiveRequestProcessed = 52,
        ItTicketResolved = 53,

        SignatureAdded = 60,
        SignatureRevoked = 61,
        DocumentLocked = 62,

        UserLogin = 90,
        UserLogout = 91,
        Other = 99
    }
}
