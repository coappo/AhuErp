using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>In-memory реализация для тестов и демо.</summary>
    public sealed class InMemoryNotificationRepository : INotificationRepository
    {
        private readonly List<Notification> _items = new List<Notification>();
        private readonly List<NotificationPreference> _prefs = new List<NotificationPreference>();
        private int _nextId;
        private int _nextPrefId;

        public Notification Add(Notification n)
        {
            if (n == null) throw new ArgumentNullException(nameof(n));
            n.Id = ++_nextId;
            _items.Add(n);
            return n;
        }

        public Notification Get(int id) => _items.FirstOrDefault(x => x.Id == id);

        public void Update(Notification n)
        {
            if (n == null) throw new ArgumentNullException(nameof(n));
            var idx = _items.FindIndex(x => x.Id == n.Id);
            if (idx >= 0) _items[idx] = n;
        }

        public IReadOnlyList<Notification> ListByRecipient(int recipientId, bool unreadOnly)
            => _items
                .Where(n => n.RecipientId == recipientId && (!unreadOnly || !n.IsRead))
                .OrderByDescending(n => n.CreatedAt)
                .ToList()
                .AsReadOnly();

        public int CountUnread(int recipientId)
            => _items.Count(n => n.RecipientId == recipientId && !n.IsRead);

        public IReadOnlyList<Notification> ListByRelatedTask(int taskId, NotificationKind kind)
            => _items
                .Where(n => n.RelatedTaskId == taskId && n.Kind == kind)
                .ToList()
                .AsReadOnly();

        public IReadOnlyList<Notification> ListByRelatedTaskAndRecipient(
            int taskId, NotificationKind kind, int recipientId)
            => _items
                .Where(n => n.RelatedTaskId == taskId
                            && n.Kind == kind
                            && n.RecipientId == recipientId)
                .ToList()
                .AsReadOnly();

        public NotificationPreference GetPreference(int employeeId, NotificationKind kind)
            => _prefs.FirstOrDefault(p => p.EmployeeId == employeeId && p.Kind == kind);

        public void SetPreference(NotificationPreference pref)
        {
            if (pref == null) throw new ArgumentNullException(nameof(pref));
            var existing = GetPreference(pref.EmployeeId, pref.Kind);
            if (existing != null)
            {
                existing.Channel = pref.Channel;
                existing.IsEnabled = pref.IsEnabled;
                existing.EmailOverride = pref.EmailOverride;
            }
            else
            {
                pref.Id = ++_nextPrefId;
                _prefs.Add(pref);
            }
        }

        public IReadOnlyList<NotificationPreference> ListPreferences(int employeeId)
            => _prefs.Where(p => p.EmployeeId == employeeId).ToList().AsReadOnly();
    }
}
