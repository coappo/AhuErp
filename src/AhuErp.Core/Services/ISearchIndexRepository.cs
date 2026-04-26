using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>Репозиторий полнотекстового индекса вложений (Phase 10).</summary>
    public interface ISearchIndexRepository
    {
        AttachmentTextIndex GetByAttachment(int attachmentId);
        IReadOnlyList<AttachmentTextIndex> ListAll();
        IReadOnlyList<AttachmentTextIndex> ListByDocument(int documentId);
        AttachmentTextIndex Add(AttachmentTextIndex entry);
        void Update(AttachmentTextIndex entry);
        void Remove(int attachmentId);
    }
}
