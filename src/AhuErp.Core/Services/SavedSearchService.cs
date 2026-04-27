using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Phase 10 — сохранённые поиски. Сериализует <see cref="DocumentSearchFilter"/>
    /// в JSON через <see cref="DataContractJsonSerializer"/> (доступен в net48
    /// без зависимости от Newtonsoft.Json — DocumentSearchFilter из чистых
    /// publics-свойств без иерархии).
    /// </summary>
    public sealed class SavedSearchService : ISavedSearchService
    {
        private readonly ISavedSearchRepository _repo;
        private readonly IAuditService _audit;

        public SavedSearchService(ISavedSearchRepository repo, IAuditService audit)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        }

        public SavedSearch Save(int ownerId, string name, DocumentSearchFilter filter, bool isShared)
        {
            if (ownerId <= 0) throw new ArgumentException("Владелец обязателен.", nameof(ownerId));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Имя обязательно.", nameof(name));
            if (filter == null) throw new ArgumentNullException(nameof(filter));

            var entry = new SavedSearch
            {
                OwnerId = ownerId,
                Name = name.Trim(),
                FilterJson = SerializeFilter(filter),
                IsShared = isShared,
                CreatedAt = DateTime.Now,
            };
            var stored = _repo.Add(entry);
            _audit.Record(AuditActionType.Created, nameof(SavedSearch), stored.Id, ownerId,
                newValues: $"Name={stored.Name}; IsShared={stored.IsShared}");
            return stored;
        }

        public IReadOnlyList<SavedSearch> ListForUser(int ownerId)
            => _repo.ListVisibleTo(ownerId);

        public SavedSearch Get(int id) => _repo.Get(id);

        public DocumentSearchFilter LoadFilter(int id)
        {
            var item = _repo.Get(id);
            if (item == null) throw new InvalidOperationException("Сохранённый поиск не найден.");
            return DeserializeFilter(item.FilterJson);
        }

        public void Delete(int id, int actorId)
        {
            var item = _repo.Get(id);
            if (item == null) return;
            if (item.OwnerId != actorId)
                throw new InvalidOperationException("Удалять сохранённый поиск может только его владелец.");
            _repo.Remove(id);
            _audit.Record(AuditActionType.Deleted, nameof(SavedSearch), id, actorId,
                oldValues: $"Name={item.Name}");
        }

        // ----------------------------------------------------------------

        internal static string SerializeFilter(DocumentSearchFilter f)
        {
            if (f == null) return null;
            var serializer = new DataContractJsonSerializer(typeof(DocumentSearchFilter));
            using (var ms = new MemoryStream())
            {
                serializer.WriteObject(ms, f);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        internal static DocumentSearchFilter DeserializeFilter(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new DocumentSearchFilter();
            var serializer = new DataContractJsonSerializer(typeof(DocumentSearchFilter));
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return (DocumentSearchFilter)serializer.ReadObject(ms);
            }
        }
    }
}
