using System;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Phase 8 — артефакт подписания документа или конкретного вложения.
    /// Хранится как бинарь (PKCS#7 / HMAC) в Base64 + метаданные сертификата.
    /// </summary>
    public class DocumentSignature
    {
        public int Id { get; set; }

        public int DocumentId { get; set; }
        public virtual Document Document { get; set; }

        /// <summary>Если подписан конкретный файл-вложение, иначе подписана карточка целиком.</summary>
        public int? AttachmentId { get; set; }
        public virtual DocumentAttachment Attachment { get; set; }

        public int SignerId { get; set; }
        public virtual Employee Signer { get; set; }

        public SignatureKind Kind { get; set; }

        public DateTime SignedAt { get; set; }

        /// <summary>SHA-256 от подписанного содержимого (карточки или файла).</summary>
        [StringLength(128)]
        public string SignedHash { get; set; }

        /// <summary>Бинарь подписи (HMAC / PKCS#7) в Base64.</summary>
        [StringLength(8000)]
        public string SignatureBlobBase64 { get; set; }

        [StringLength(512)]
        public string CertificateThumbprint { get; set; }

        [StringLength(256)]
        public string CertificateSubject { get; set; }

        public DateTime? CertificateNotAfter { get; set; }

        [StringLength(1024)]
        public string Reason { get; set; }

        public bool IsRevoked { get; set; }

        public DateTime? RevokedAt { get; set; }
    }
}
