using System;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Phase 10 — индекс полного текста, извлечённого из вложения.
    /// Денормализует <see cref="DocumentId"/>, чтобы поиск шёл по индексу
    /// без джойна на <see cref="DocumentAttachment"/>. Запись считается
    /// «свежей», если <see cref="SourceContentHash"/> совпадает с
    /// <see cref="DocumentAttachment.Hash"/>.
    /// </summary>
    public class AttachmentTextIndex
    {
        public int Id { get; set; }
        public int AttachmentId { get; set; }
        public virtual DocumentAttachment Attachment { get; set; }
        public int DocumentId { get; set; }

        /// <summary>
        /// Извлечённый текст вложения. Хранится как nvarchar(max).
        /// Может быть null, если экстрактор не справился (Stream, картинка, ZIP).
        /// </summary>
        public string ExtractedText { get; set; }

        public DateTime IndexedAt { get; set; }

        [StringLength(64)]
        public string SourceContentHash { get; set; }
    }
}
