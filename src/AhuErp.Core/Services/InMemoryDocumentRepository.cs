using System;
using System.Collections.Generic;
using System.Linq;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// In-memory реализация документа, сохраняющая порядок добавления и
    /// присваивающая инкрементальный Id. Служит и для тестов, и для демо-режима.
    /// </summary>
    public sealed class InMemoryDocumentRepository : IDocumentRepository
    {
        private readonly List<Document> _documents = new List<Document>();
        private int _nextId = 1;

        public IReadOnlyList<Document> ListByType(DocumentType type)
        {
            return _documents.Where(d => d.Type == type
                                         && !(d is ArchiveRequest)
                                         && !(d is ItTicket))
                             .ToList()
                             .AsReadOnly();
        }

        public IReadOnlyList<ArchiveRequest> ListArchiveRequests()
        {
            return _documents.OfType<ArchiveRequest>().ToList().AsReadOnly();
        }

        public IReadOnlyList<ItTicket> ListItTickets()
        {
            return _documents.OfType<ItTicket>().ToList().AsReadOnly();
        }

        public IReadOnlyList<Document> ListInventoryEligibleDocuments()
        {
            return _documents.Where(d => d.Type == DocumentType.Internal
                                         || d.Type == DocumentType.It
                                         || d is ItTicket)
                             .ToList()
                             .AsReadOnly();
        }

        public Document GetById(int id) => _documents.FirstOrDefault(d => d.Id == id);

        public void Add(Document document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (document.Id == 0) document.Id = _nextId++;
            else _nextId = Math.Max(_nextId, document.Id + 1);
            _documents.Add(document);
        }

        public void Update(Document document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            var index = _documents.FindIndex(d => d.Id == document.Id);
            if (index < 0)
                throw new InvalidOperationException($"Документ #{document.Id} не найден.");
            _documents[index] = document;
        }

        public void Remove(int id)
        {
            var index = _documents.FindIndex(d => d.Id == id);
            if (index >= 0) _documents.RemoveAt(index);
        }
    }
}
