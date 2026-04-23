using System;
using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// In-memory реализация <see cref="IDashboardService"/> — выполняет агрегацию
    /// над переданной коллекцией документов, без обращения к БД.
    /// </summary>
    public class DashboardService : IDashboardService
    {
        public int CountOverdue(IEnumerable<Document> documents, DateTime now)
        {
            if (documents == null) throw new ArgumentNullException(nameof(documents));

            var count = 0;
            foreach (var doc in documents)
            {
                if (doc == null) continue;
                if (doc.IsOverdue(now)) count++;
            }
            return count;
        }

        public int CountDueSoon(IEnumerable<Document> documents, DateTime now, int daysThreshold = 3)
        {
            if (documents == null) throw new ArgumentNullException(nameof(documents));
            if (daysThreshold < 0) throw new ArgumentOutOfRangeException(nameof(daysThreshold));

            var horizon = now.AddDays(daysThreshold);
            var count = 0;
            foreach (var doc in documents)
            {
                if (doc == null) continue;
                if (doc.Status == DocumentStatus.Completed || doc.Status == DocumentStatus.Cancelled) continue;
                if (doc.Deadline >= now && doc.Deadline <= horizon) count++;
            }
            return count;
        }
    }
}
