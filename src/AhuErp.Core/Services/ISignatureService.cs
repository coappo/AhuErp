using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Phase 8 — фасад над <see cref="ISignatureRepository"/> + <see cref="ICryptoProvider"/>.
    /// </summary>
    public interface ISignatureService
    {
        DocumentSignature Sign(int documentId, int? attachmentId, int signerId,
                               SignatureKind kind, string reason = null,
                               string certificateThumbprint = null);

        bool Verify(int signatureId);

        void Revoke(int signatureId, int actorId, string reason);

        /// <summary>Отозвать все ПЭП/НЭП по документу — вызывается после загрузки новой версии вложения.</summary>
        int RevokeAllNonQualified(int documentId, int actorId, string reason);

        IReadOnlyList<DocumentSignature> ListByDocument(int documentId);

        bool IsDocumentSigned(int documentId, SignatureKind minKind = SignatureKind.Simple);
    }
}
