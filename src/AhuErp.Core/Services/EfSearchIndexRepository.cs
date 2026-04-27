using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    public sealed class EfSearchIndexRepository : ISearchIndexRepository
    {
        private readonly AhuDbContext _ctx;

        public EfSearchIndexRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public AttachmentTextIndex GetByAttachment(int attachmentId)
            => _ctx.AttachmentTextIndices.FirstOrDefault(x => x.AttachmentId == attachmentId);

        public IReadOnlyList<AttachmentTextIndex> ListAll()
            => _ctx.AttachmentTextIndices.ToList().AsReadOnly();

        public IReadOnlyList<AttachmentTextIndex> ListByDocument(int documentId)
            => _ctx.AttachmentTextIndices.Where(x => x.DocumentId == documentId)
                .ToList().AsReadOnly();

        public AttachmentTextIndex Add(AttachmentTextIndex entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            _ctx.AttachmentTextIndices.Add(entry);
            _ctx.SaveChanges();
            return entry;
        }

        public void Update(AttachmentTextIndex entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            _ctx.Entry(entry).State = EntityState.Modified;
            _ctx.SaveChanges();
        }

        public void Remove(int attachmentId)
        {
            var existing = _ctx.AttachmentTextIndices
                .FirstOrDefault(x => x.AttachmentId == attachmentId);
            if (existing != null)
            {
                _ctx.AttachmentTextIndices.Remove(existing);
                _ctx.SaveChanges();
            }
        }
    }
}
