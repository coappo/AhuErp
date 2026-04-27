using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    public sealed class InMemorySignatureRepository : ISignatureRepository
    {
        private readonly List<DocumentSignature> _items = new List<DocumentSignature>();
        private int _nextId;

        public DocumentSignature Add(DocumentSignature s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            s.Id = ++_nextId;
            _items.Add(s);
            return s;
        }

        public DocumentSignature Get(int id) => _items.FirstOrDefault(x => x.Id == id);

        public void Update(DocumentSignature s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            var idx = _items.FindIndex(x => x.Id == s.Id);
            if (idx >= 0) _items[idx] = s;
        }

        public IReadOnlyList<DocumentSignature> ListByDocument(int documentId)
            => _items.Where(s => s.DocumentId == documentId)
                .OrderBy(s => s.SignedAt).ToList().AsReadOnly();

        public IReadOnlyList<DocumentSignature> ListByAttachment(int attachmentId)
            => _items.Where(s => s.AttachmentId == attachmentId)
                .OrderBy(s => s.SignedAt).ToList().AsReadOnly();
    }
}
