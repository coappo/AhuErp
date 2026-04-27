namespace AhuErp.Core.Models
{
    /// <summary>
    /// Область действия замещения (Phase 11): что именно перенаправляется
    /// заместителю при наличии активной записи <see cref="Substitution"/>.
    /// </summary>
    public enum SubstitutionScope
    {
        /// <summary>Только новые поручения (TaskService.CreateTask).</summary>
        TasksOnly = 0,

        /// <summary>Только новые согласования (ApprovalService.StartApproval).</summary>
        ApprovalsOnly = 1,

        /// <summary>И поручения, и согласования.</summary>
        Full = 2
    }
}
