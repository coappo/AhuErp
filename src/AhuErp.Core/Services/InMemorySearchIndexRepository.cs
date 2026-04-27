using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    public sealed class InMemorySearchIndexRepository : ISearchIndexRepository
    {
        private readonly List<AttachmentTextIndex> _items = new List<AttachmentTextIndex>();
        private int _nextId;

        public AttachmentTextIndex GetByAttachment(int attachmentId)
            => _items.FirstOrDefault(x => x.AttachmentId == attachmentId);

        public IReadOnlyList<AttachmentTextIndex> ListAll()
            => _items.ToList().AsReadOnly();

        public IReadOnlyList<AttachmentTextIndex> ListByDocument(int documentId)
            => _items.Where(x => x.DocumentId == documentId).ToList().AsReadOnly();

        public AttachmentTextIndex Add(AttachmentTextIndex entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            entry.Id = ++_nextId;
            _items.Add(entry);
            return entry;
        }

        public void Update(AttachmentTextIndex entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            var idx = _items.FindIndex(x => x.Id == entry.Id);
            if (idx >= 0) _items[idx] = entry;
        }

        public void Remove(int attachmentId)
            => _items.RemoveAll(x => x.AttachmentId == attachmentId);
    }
}
