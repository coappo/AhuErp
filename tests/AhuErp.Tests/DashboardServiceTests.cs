using System;
using System.Collections.Generic;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using Xunit;

namespace AhuErp.Tests
{
    public class DashboardServiceTests
    {
        private readonly DashboardService _service = new DashboardService();
        private static readonly DateTime Now = new DateTime(2026, 4, 23, 12, 0, 0);

        private static Document Doc(DateTime deadline, DocumentStatus status)
        {
            return new Document
            {
                Title = "Doc",
                Type = DocumentType.General,
                CreationDate = deadline.AddDays(-10),
                Deadline = deadline,
                Status = status
            };
        }

        [Fact]
        public void CountOverdue_only_counts_past_deadline_with_active_status()
        {
            var docs = new List<Document>
            {
                Doc(Now.AddDays(-5), DocumentStatus.New),         // overdue
                Doc(Now.AddDays(-1), DocumentStatus.InProgress),  // overdue
                Doc(Now.AddDays(-2), DocumentStatus.OnHold),      // overdue
                Doc(Now.AddDays(-2), DocumentStatus.Completed),   // не считать: завершён
                Doc(Now.AddDays(-2), DocumentStatus.Cancelled),   // не считать: отменён
                Doc(Now.AddDays(+3), DocumentStatus.New),         // срок в будущем
                Doc(Now,              DocumentStatus.New),        // ровно сейчас — не просрочен
            };

            Assert.Equal(3, _service.CountOverdue(docs, Now));
        }

        [Fact]
        public void CountOverdue_returns_zero_for_empty_collection()
        {
            Assert.Equal(0, _service.CountOverdue(Array.Empty<Document>(), Now));
        }

        [Fact]
        public void CountOverdue_throws_on_null_collection()
        {
            Assert.Throws<ArgumentNullException>(() => _service.CountOverdue(null, Now));
        }

        [Fact]
        public void CountDueSoon_counts_within_threshold_window_only()
        {
            var docs = new List<Document>
            {
                Doc(Now.AddDays(1), DocumentStatus.New),        // <3 days → due soon
                Doc(Now.AddDays(2), DocumentStatus.InProgress), // due soon
                Doc(Now.AddDays(3), DocumentStatus.New),        // ровно 3 дня — на границе, считаем
                Doc(Now.AddDays(4), DocumentStatus.New),        // дальше порога
                Doc(Now.AddDays(-1), DocumentStatus.New),       // уже просрочен — не «due soon»
                Doc(Now.AddDays(1), DocumentStatus.Completed),  // завершён — игнор
            };

            Assert.Equal(3, _service.CountDueSoon(docs, Now, daysThreshold: 3));
        }

        [Fact]
        public void CountDueSoon_respects_custom_threshold()
        {
            var docs = new List<Document>
            {
                Doc(Now.AddDays(1), DocumentStatus.New),
                Doc(Now.AddDays(5), DocumentStatus.New),
                Doc(Now.AddDays(10), DocumentStatus.New),
            };

            Assert.Equal(1, _service.CountDueSoon(docs, Now, daysThreshold: 3));
            Assert.Equal(2, _service.CountDueSoon(docs, Now, daysThreshold: 7));
        }

        [Fact]
        public void CountDueSoon_throws_on_negative_threshold()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => _service.CountDueSoon(Array.Empty<Document>(), Now, daysThreshold: -1));
        }

        [Fact]
        public void Document_IsOverdue_matches_service_semantics()
        {
            var overdue = Doc(Now.AddDays(-1), DocumentStatus.New);
            var onTime = Doc(Now.AddDays(+1), DocumentStatus.New);
            var completedPast = Doc(Now.AddDays(-1), DocumentStatus.Completed);

            Assert.True(overdue.IsOverdue(Now));
            Assert.False(onTime.IsOverdue(Now));
            Assert.False(completedPast.IsOverdue(Now));
        }
    }
}
