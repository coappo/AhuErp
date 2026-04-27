using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Phase 10. Реализация полнотекстового поиска по содержимому вложений.
    ///
    /// Извлечение: цепочка <see cref="ITextExtractor"/> по расширению файла —
    /// первый <see cref="ITextExtractor.CanHandle"/>=true получает поток.
    ///
    /// Поиск: запрос разбивается на токены (буквы/цифры/русские буквы Юникода),
    /// все токены lowercased; hit = AND по всем токенам, score = сумма частот.
    /// Snippet — окно ±80 символов вокруг первого вхождения первого токена.
    /// Это намеренно простой in-process поиск: пакета Lucene/CSP-зависимости
    /// нет, морфологию (русскую) обеспечим примитивным стеммером Портера
    /// (см. <see cref="StemRussian"/>) — он покрывает базовые окончания
    /// падежей/чисел и достаточен для МКУ-сценариев.
    /// </summary>
    public sealed class SearchIndexService : ISearchIndexService
    {
        private readonly ISearchIndexRepository _repo;
        private readonly IAttachmentRepository _attachments;
        private readonly IDocumentRepository _documents;
        private readonly IFileStorageService _storage;
        private readonly IReadOnlyList<ITextExtractor> _extractors;

        public SearchIndexService(
            ISearchIndexRepository repo,
            IAttachmentRepository attachments,
            IDocumentRepository documents,
            IFileStorageService storage,
            IEnumerable<ITextExtractor> extractors)
        {
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _attachments = attachments ?? throw new ArgumentNullException(nameof(attachments));
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            if (extractors == null) throw new ArgumentNullException(nameof(extractors));
            _extractors = extractors.ToList().AsReadOnly();
        }

        public AttachmentTextIndex IndexAttachment(int attachmentId)
        {
            var att = _attachments.GetById(attachmentId);
            if (att == null) return null;

            string text = ExtractText(att);
            var existing = _repo.GetByAttachment(attachmentId);
            if (existing != null)
            {
                existing.ExtractedText = text;
                existing.IndexedAt = DateTime.Now;
                existing.SourceContentHash = att.Hash;
                _repo.Update(existing);
                return existing;
            }

            var entry = new AttachmentTextIndex
            {
                AttachmentId = attachmentId,
                DocumentId = att.DocumentId,
                ExtractedText = text,
                IndexedAt = DateTime.Now,
                SourceContentHash = att.Hash,
            };
            return _repo.Add(entry);
        }

        public int ReindexAll()
        {
            // Repository не возвращает «все вложения»; пройдёмся через ListByDocument.
            var docs = _documents.Search(new DocumentSearchFilter());
            int count = 0;
            foreach (var d in docs)
            {
                foreach (var a in _attachments.ListByDocument(d.Id))
                {
                    if (IndexAttachment(a.Id) != null) count++;
                }
            }
            return count;
        }

        public int IndexOutdated()
        {
            int count = 0;
            foreach (var existing in _repo.ListAll())
            {
                var att = _attachments.GetById(existing.AttachmentId);
                if (att == null) continue;
                if (!string.Equals(existing.SourceContentHash, att.Hash, StringComparison.Ordinal))
                {
                    if (IndexAttachment(att.Id) != null) count++;
                }
            }
            return count;
        }

        public IReadOnlyList<SearchHit> FullTextSearch(string query, int? maxResults = 100)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Array.Empty<SearchHit>();

            var tokens = Tokenize(query).Select(StemRussian).Distinct().ToList();
            if (tokens.Count == 0) return Array.Empty<SearchHit>();

            var hits = new List<SearchHit>();
            foreach (var entry in _repo.ListAll())
            {
                if (string.IsNullOrEmpty(entry.ExtractedText)) continue;
                var lower = entry.ExtractedText.ToLowerInvariant();

                int totalScore = 0;
                int firstOffset = -1;
                bool allMatch = true;
                foreach (var t in tokens)
                {
                    int idx = lower.IndexOf(t, StringComparison.Ordinal);
                    if (idx < 0) { allMatch = false; break; }
                    if (firstOffset < 0 || idx < firstOffset) firstOffset = idx;
                    int occurrences = CountOccurrences(lower, t);
                    totalScore += occurrences;
                }
                if (!allMatch) continue;

                var att = _attachments.GetById(entry.AttachmentId);
                var doc = _documents.GetById(entry.DocumentId);
                hits.Add(new SearchHit
                {
                    DocumentId = entry.DocumentId,
                    DocumentTitle = doc?.Title,
                    RegistrationNumber = doc?.RegistrationNumber,
                    AttachmentId = entry.AttachmentId,
                    AttachmentName = att?.FileName,
                    Snippet = BuildSnippet(entry.ExtractedText, firstOffset, 80),
                    Score = totalScore,
                });
            }

            IEnumerable<SearchHit> ordered = hits.OrderByDescending(h => h.Score).ThenBy(h => h.DocumentId);
            if (maxResults.HasValue) ordered = ordered.Take(maxResults.Value);
            return ordered.ToList().AsReadOnly();
        }

        // ----------------------------------------------------------------

        private string ExtractText(DocumentAttachment att)
        {
            if (att == null || string.IsNullOrEmpty(att.StoragePath)) return string.Empty;
            try
            {
                if (!_storage.Exists(att.StoragePath)) return string.Empty;
                using (var s = _storage.Open(att.StoragePath))
                {
                    var extractor = _extractors.FirstOrDefault(e => e.CanHandle(att.FileName));
                    if (extractor == null) return string.Empty;
                    return extractor.Extract(s) ?? string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        internal static IEnumerable<string> Tokenize(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) yield break;
            var sb = new StringBuilder();
            foreach (var ch in text)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    sb.Append(char.ToLowerInvariant(ch));
                }
                else if (sb.Length > 0)
                {
                    if (sb.Length >= 2) yield return sb.ToString();
                    sb.Clear();
                }
            }
            if (sb.Length >= 2) yield return sb.ToString();
        }

        /// <summary>
        /// Примитивный русский стеммер: отрезает несколько типичных окончаний.
        /// Не претендует на полноту, но снимает падежные окончания «договор/договора/договорам»,
        /// «приказ/приказы/приказе», «служебная/служебной/служебные».
        /// Для латиницы — без изменений.
        /// </summary>
        internal static string StemRussian(string token)
        {
            if (string.IsNullOrEmpty(token) || token.Length <= 4) return token;
            string[] suffixes =
            {
                "иями", "ями", "ами", "ими", "ыми", "ого", "его", "ому", "ему",
                "ыми", "ого", "ой", "ей", "ия", "ии", "ие", "ию", "ия", "ям", "ах",
                "ов", "ев", "ый", "ий", "ая", "яя", "ое", "ее", "ые", "ие",
                "у", "ю", "ы", "и", "а", "я", "е", "ь",
            };
            foreach (var s in suffixes)
            {
                if (token.Length - s.Length >= 3 && token.EndsWith(s, StringComparison.Ordinal))
                {
                    return token.Substring(0, token.Length - s.Length);
                }
            }
            return token;
        }

        internal static int CountOccurrences(string haystack, string needle)
        {
            if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle)) return 0;
            int count = 0, idx = 0;
            while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += needle.Length;
            }
            return count;
        }

        internal static string BuildSnippet(string text, int offset, int radius)
        {
            if (string.IsNullOrEmpty(text) || offset < 0) return string.Empty;
            int start = Math.Max(0, offset - radius);
            int end = Math.Min(text.Length, offset + radius);
            string snippet = text.Substring(start, end - start);
            // нормализуем переносы.
            return snippet.Replace('\r', ' ').Replace('\n', ' ').Trim();
        }
    }
}
