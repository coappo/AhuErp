using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>EF6-реализация <see cref="INotificationRepository"/>.</summary>
    public sealed class EfNotificationRepository : INotificationRepository
    {
        private readonly AhuDbContext _ctx;

        public EfNotificationRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public Notification Add(Notification n)
        {
            if (n == null) throw new ArgumentNullException(nameof(n));
            _ctx.Notifications.Add(n);
            _ctx.SaveChanges();
            return n;
        }

        public Notification Get(int id) => _ctx.Notifications.Find(id);

        public void Update(Notification n)
        {
            if (n == null) throw new ArgumentNullException(nameof(n));
            _ctx.Entry(n).State = EntityState.Modified;
            _ctx.SaveChanges();
        }

        public IReadOnlyList<Notification> ListByRecipient(int recipientId, bool unreadOnly)
        {
            IQueryable<Notification> q = _ctx.Notifications.Where(n => n.RecipientId == recipientId);
            if (unreadOnly) q = q.Where(n => n.ReadAt == null);
            return q.OrderByDescending(n => n.CreatedAt).ToList().AsReadOnly();
        }

        public int CountUnread(int recipientId)
            => _ctx.Notifications.Count(n => n.RecipientId == recipientId && n.ReadAt == null);

        public IReadOnlyList<Notification> ListByRelatedTask(int taskId, NotificationKind kind)
            => _ctx.Notifications
                .Where(n => n.RelatedTaskId == taskId && n.Kind == kind)
                .ToList()
                .AsReadOnly();

        public NotificationPreference GetPreference(int employeeId, NotificationKind kind)
            => _ctx.NotificationPreferences
                .FirstOrDefault(p => p.EmployeeId == employeeId && p.Kind == kind);

        public void SetPreference(NotificationPreference pref)
        {
            if (pref == null) throw new ArgumentNullException(nameof(pref));
            var existing = GetPreference(pref.EmployeeId, pref.Kind);
            if (existing != null)
            {
                existing.Channel = pref.Channel;
                existing.IsEnabled = pref.IsEnabled;
                existing.EmailOverride = pref.EmailOverride;
                _ctx.Entry(existing).State = EntityState.Modified;
            }
            else
            {
                _ctx.NotificationPreferences.Add(pref);
            }
            _ctx.SaveChanges();
        }

        public IReadOnlyList<NotificationPreference> ListPreferences(int employeeId)
            => _ctx.NotificationPreferences
                .Where(p => p.EmployeeId == employeeId)
                .ToList()
                .AsReadOnly();
    }
}
