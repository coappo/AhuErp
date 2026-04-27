using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    public sealed class EfSignatureRepository : ISignatureRepository
    {
        private readonly AhuDbContext _ctx;

        public EfSignatureRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public DocumentSignature Add(DocumentSignature s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            _ctx.DocumentSignatures.Add(s);
            _ctx.SaveChanges();
            return s;
        }

        public DocumentSignature Get(int id) => _ctx.DocumentSignatures.Find(id);

        public void Update(DocumentSignature s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            _ctx.Entry(s).State = EntityState.Modified;
            _ctx.SaveChanges();
        }

        public IReadOnlyList<DocumentSignature> ListByDocument(int documentId)
            => _ctx.DocumentSignatures.Where(s => s.DocumentId == documentId)
                .OrderBy(s => s.SignedAt).ToList().AsReadOnly();

        public IReadOnlyList<DocumentSignature> ListByAttachment(int attachmentId)
            => _ctx.DocumentSignatures.Where(s => s.AttachmentId == attachmentId)
                .OrderBy(s => s.SignedAt).ToList().AsReadOnly();
    }
}
