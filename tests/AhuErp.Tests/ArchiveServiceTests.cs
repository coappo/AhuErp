using System;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using Xunit;

namespace AhuErp.Tests
{
    public class ArchiveServiceTests
    {
        private readonly ArchiveService _service = new ArchiveService();

        [Fact]
        public void CreateRequest_sets_deadline_to_creation_plus_30_days()
        {
            var created = new DateTime(2026, 4, 23, 12, 0, 0);

            var request = _service.CreateRequest("Справка о трудовом стаже", created);

            Assert.Equal(created, request.CreationDate);
            Assert.Equal(created.AddDays(30), request.Deadline);
            Assert.Equal(DocumentStatus.New, request.Status);
            Assert.Equal(DocumentType.Archive, request.Type);
        }

        [Fact]
        public void CreateRequest_rejects_empty_title()
        {
            Assert.Throws<ArgumentException>(
                () => _service.CreateRequest("   ", DateTime.UtcNow));
        }

        [Fact]
        public void CanCompleteRequest_returns_false_when_both_scans_missing()
        {
            var request = _service.CreateRequest("A", DateTime.UtcNow);
            Assert.False(request.CanCompleteRequest());
        }

        [Fact]
        public void CanCompleteRequest_returns_false_when_only_passport_present()
        {
            var request = _service.CreateRequest("A", DateTime.UtcNow);
            request.HasPassportScan = true;
            Assert.False(request.CanCompleteRequest());
        }

        [Fact]
        public void CanCompleteRequest_returns_false_when_only_workbook_present()
        {
            var request = _service.CreateRequest("A", DateTime.UtcNow);
            request.HasWorkBookScan = true;
            Assert.False(request.CanCompleteRequest());
        }

        [Fact]
        public void CanCompleteRequest_returns_true_when_both_scans_present()
        {
            var request = _service.CreateRequest("A", DateTime.UtcNow);
            request.HasPassportScan = true;
            request.HasWorkBookScan = true;
            Assert.True(request.CanCompleteRequest());
        }

        [Fact]
        public void CompleteRequest_throws_when_prerequisites_not_met()
        {
            var request = _service.CreateRequest("A", DateTime.UtcNow);
            request.HasPassportScan = true;
            // trudovaya отсутствует

            Assert.Throws<InvalidOperationException>(() => _service.CompleteRequest(request));
            Assert.NotEqual(DocumentStatus.Completed, request.Status);
        }

        [Fact]
        public void CompleteRequest_marks_completed_when_prerequisites_met()
        {
            var request = _service.CreateRequest("A", DateTime.UtcNow);
            request.HasPassportScan = true;
            request.HasWorkBookScan = true;

            _service.CompleteRequest(request);

            Assert.Equal(DocumentStatus.Completed, request.Status);
        }
    }
}
