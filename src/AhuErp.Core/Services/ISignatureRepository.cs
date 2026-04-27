using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    public interface ISignatureRepository
    {
        DocumentSignature Add(DocumentSignature s);
        DocumentSignature Get(int id);
        void Update(DocumentSignature s);
        IReadOnlyList<DocumentSignature> ListByDocument(int documentId);
        IReadOnlyList<DocumentSignature> ListByAttachment(int attachmentId);
    }
}
