using System;
using System.Linq;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Phase 10. Сохранённые поиски: CRUD, JSON-roundtrip, видимость
    /// (свои + shared), запрет удаления чужих.
    /// </summary>
    public class SavedSearchServiceTests
    {
        private readonly InMemorySavedSearchRepository _repo = new InMemorySavedSearchRepository();
        private readonly InMemoryAuditLogRepository _auditRepo = new InMemoryAuditLogRepository();
        private readonly SavedSearchService _service;

        public SavedSearchServiceTests()
        {
            _service = new SavedSearchService(_repo, new AuditService(_auditRepo));
        }

        [Fact]
        public void Save_persists_entry_and_writes_audit()
        {
            var filter = new DocumentSearchFilter { Text = "договор" };
            var saved = _service.Save(ownerId: 1, name: "Мои договоры", filter: filter, isShared: false);

            Assert.True(saved.Id > 0);
            Assert.Equal("Мои договоры", saved.Name);
            Assert.False(saved.IsShared);
            Assert.False(string.IsNullOrEmpty(saved.FilterJson));
            Assert.Single(_auditRepo.ListAllOrdered(),
                a => a.ActionType == AuditActionType.Created && a.EntityType == nameof(SavedSearch));
        }

        [Fact]
        public void LoadFilter_roundtrips_json()
        {
            var filter = new DocumentSearchFilter
            {
                Text = "приказ",
                Direction = DocumentDirection.Internal,
                Status = DocumentStatus.InProgress,
                From = new DateTime(2026, 1, 1),
                To = new DateTime(2026, 12, 31),
            };
            var saved = _service.Save(1, "Фильтр", filter, isShared: false);

            var loaded = _service.LoadFilter(saved.Id);
            Assert.Equal(filter.Text, loaded.Text);
            Assert.Equal(filter.Direction, loaded.Direction);
            Assert.Equal(filter.Status, loaded.Status);
            Assert.Equal(filter.From, loaded.From);
            Assert.Equal(filter.To, loaded.To);
        }

        [Fact]
        public void ListForUser_returns_own_and_shared()
        {
            _service.Save(ownerId: 1, name: "Свой A", filter: new DocumentSearchFilter(), isShared: false);
            _service.Save(ownerId: 2, name: "Чужой B", filter: new DocumentSearchFilter(), isShared: false);
            _service.Save(ownerId: 2, name: "Общий C", filter: new DocumentSearchFilter(), isShared: true);

            var visibleTo1 = _service.ListForUser(ownerId: 1);
            var names = visibleTo1.Select(s => s.Name).ToList();

            Assert.Contains("Свой A", names);
            Assert.Contains("Общий C", names);
            Assert.DoesNotContain("Чужой B", names);
        }

        [Fact]
        public void Delete_only_owner_can_remove()
        {
            var saved = _service.Save(ownerId: 1, name: "X", filter: new DocumentSearchFilter(), isShared: false);

            Assert.Throws<InvalidOperationException>(() =>
                _service.Delete(saved.Id, actorId: 2));

            // Запись осталась.
            Assert.NotNull(_service.Get(saved.Id));

            _service.Delete(saved.Id, actorId: 1);
            Assert.Null(_service.Get(saved.Id));
        }

        [Fact]
        public void Save_validates_required_fields()
        {
            Assert.Throws<ArgumentException>(() =>
                _service.Save(ownerId: 0, "Имя", new DocumentSearchFilter(), false));
            Assert.Throws<ArgumentException>(() =>
                _service.Save(ownerId: 1, name: "  ", new DocumentSearchFilter(), false));
            Assert.Throws<ArgumentNullException>(() =>
                _service.Save(ownerId: 1, name: "Имя", filter: null, false));
        }
    }
}
