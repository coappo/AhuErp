namespace AhuErp.Core.Models
{
    /// <summary>
    /// Phase 9 — канал доставки уведомления. <see cref="Both"/> = и in-app,
    /// и email; <see cref="Email"/> — только e-mail (in-app не пишется).
    /// </summary>
    public enum NotificationChannel
    {
        InApp = 0,
        Email = 1,
        Both = 2,
    }
}
