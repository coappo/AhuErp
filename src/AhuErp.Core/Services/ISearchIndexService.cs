using System.Collections.Generic;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Phase 10. Сервис полнотекстового индекса по содержимому вложений.
    /// </summary>
    public interface ISearchIndexService
    {
        /// <summary>Извлечь и проиндексировать текст одного вложения. Возвращает запись индекса (или null, если файл недоступен).</summary>
        AhuErp.Core.Models.AttachmentTextIndex IndexAttachment(int attachmentId);

        /// <summary>Полная переиндексация всех вложений (медленно — для админ-операций).</summary>
        int ReindexAll();

        /// <summary>Переиндексировать только записи, у которых SourceContentHash != Attachment.Hash.</summary>
        int IndexOutdated();

        /// <summary>Поиск по индексу. Возвращает hits, отсортированные по Score DESC.</summary>
        IReadOnlyList<SearchHit> FullTextSearch(string query, int? maxResults = 100);
    }
}
