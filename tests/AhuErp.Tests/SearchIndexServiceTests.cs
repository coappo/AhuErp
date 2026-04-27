using System;
using System.IO;
using System.Linq;
using System.Text;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Phase 10. Полнотекстовый индекс по содержимому вложений.
    /// </summary>
    public class SearchIndexServiceTests
    {
        private readonly InMemoryDocumentRepository _docs = new InMemoryDocumentRepository();
        private readonly InMemoryAttachmentRepository _attachments = new InMemoryAttachmentRepository();
        private readonly InMemoryFileStorageService _storage = new InMemoryFileStorageService();
        private readonly InMemorySearchIndexRepository _indices = new InMemorySearchIndexRepository();
        private readonly SearchIndexService _service;

        public SearchIndexServiceTests()
        {
            _service = new SearchIndexService(
                _indices, _attachments, _docs, _storage,
                new ITextExtractor[] { new PlainTextExtractor() });
        }

        private (Document doc, DocumentAttachment att) Seed(string fileName, string text,
            string title = "Документ", string regNo = null)
        {
            var doc = new Document
            {
                Title = title,
                Type = DocumentType.Office,
                CreationDate = DateTime.Now,
                Deadline = DateTime.Now.AddDays(7),
                RegistrationNumber = regNo ?? $"ИСХ-2026-{_docs.GetType().Name}-{Guid.NewGuid():N}".Substring(0, 24),
            };
            _docs.Add(doc);

            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(text)))
            {
                var path = _storage.Store(ms, doc.RegistrationNumber, version: 1, fileName);
                var att = _attachments.Add(new DocumentAttachment
                {
                    DocumentId = doc.Id,
                    FileName = fileName,
                    StoragePath = path,
                    VersionNumber = 1,
                    IsCurrentVersion = true,
                    UploadedAt = DateTime.UtcNow,
                    UploadedById = 1,
                    Hash = "h-" + text.Length,
                    SizeBytes = text.Length,
                    FileType = AttachmentKind.Draft,
                });
                att.AttachmentGroupId = att.Id;
                _attachments.Update(att);
                return (doc, att);
            }
        }

        [Fact]
        public void IndexAttachment_extracts_plain_text_and_stores_hash()
        {
            var (doc, att) = Seed("note.txt", "Договор поставки канцелярских товаров.");
            var entry = _service.IndexAttachment(att.Id);

            Assert.NotNull(entry);
            Assert.Equal(att.Id, entry.AttachmentId);
            Assert.Equal(doc.Id, entry.DocumentId);
            Assert.Contains("Договор", entry.ExtractedText);
            Assert.Equal(att.Hash, entry.SourceContentHash);
        }

        [Fact]
        public void IndexAttachment_returns_null_for_unknown_attachment()
        {
            Assert.Null(_service.IndexAttachment(attachmentId: 999));
        }

        [Fact]
        public void IndexAttachment_returns_empty_text_when_no_extractor_handles_file()
        {
            var (_, att) = Seed("photo.jpg", "binary-blob");
            var entry = _service.IndexAttachment(att.Id);
            Assert.NotNull(entry);
            Assert.Equal(string.Empty, entry.ExtractedText);
        }

        [Fact]
        public void IndexAttachment_is_idempotent_and_updates_on_rerun()
        {
            var (_, att) = Seed("doc.txt", "Старый текст служебной записки.");
            var first = _service.IndexAttachment(att.Id);

            // Подменяем содержимое: «новая версия».
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes("Новый текст приказа.")))
            {
                _storage.Store(ms, "x", 1, att.FileName);
            }
            // Внутри тестового storage Store() переписывает по тому же ключу
            // только при совпадении пути — поэтому проще обновить хэш в репо.
            att.Hash = "h-new";
            _attachments.Update(att);

            var second = _service.IndexAttachment(att.Id);
            Assert.Equal(first.Id, second.Id);
            Assert.Equal("h-new", second.SourceContentHash);
        }

        [Fact]
        public void IndexOutdated_reindexes_only_changed_attachments()
        {
            var (_, a1) = Seed("a.txt", "alpha");
            var (_, a2) = Seed("b.txt", "beta");
            _service.IndexAttachment(a1.Id);
            _service.IndexAttachment(a2.Id);

            // У a2 «изменился» файл — хэш расходится.
            a2.Hash = "h-changed";
            _attachments.Update(a2);

            var rebuilt = _service.IndexOutdated();
            Assert.Equal(1, rebuilt);
        }

        [Fact]
        public void IndexOutdated_indexes_new_attachments_without_index_entry()
        {
            // Сценарий: пользователь загрузил вложение, индексной записи ещё нет
            // (Upload/AddVersion не дёргают IndexAttachment). Тик `IndexOutdated()`
            // должен сам обнаружить такое вложение и проиндексировать его.
            var (_, fresh) = Seed("fresh.txt", "новое содержимое для индексации");
            Assert.Empty(_indices.ListAll());

            var indexed = _service.IndexOutdated();

            Assert.Equal(1, indexed);
            var entry = _indices.GetByAttachment(fresh.Id);
            Assert.NotNull(entry);
            Assert.Contains("новое", entry.ExtractedText);
        }

        [Fact]
        public void FullTextSearch_returns_hits_ordered_by_score()
        {
            var (d1, a1) = Seed("a.txt", "договор поставки канцтоваров", regNo: "INT-1");
            var (d2, a2) = Seed("b.txt", "Договор. Снова договор. Третий раз договор.", regNo: "INT-2");
            var (d3, a3) = Seed("c.txt", "приказ о премировании", regNo: "INT-3");
            _service.IndexAttachment(a1.Id);
            _service.IndexAttachment(a2.Id);
            _service.IndexAttachment(a3.Id);

            var hits = _service.FullTextSearch("договор");

            Assert.Equal(2, hits.Count);
            Assert.Equal(d2.Id, hits[0].DocumentId); // больше вхождений → выше score
            Assert.Equal(d1.Id, hits[1].DocumentId);
            Assert.Contains("договор", hits[0].Snippet, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void FullTextSearch_requires_all_tokens()
        {
            var (_, a1) = Seed("a.txt", "договор поставки", regNo: "INT-1");
            var (d2, a2) = Seed("b.txt", "договор подряда", regNo: "INT-2");
            _service.IndexAttachment(a1.Id);
            _service.IndexAttachment(a2.Id);

            var hits = _service.FullTextSearch("договор подряда");

            Assert.Single(hits);
            Assert.Equal(d2.Id, hits[0].DocumentId);
        }

        [Fact]
        public void FullTextSearch_returns_empty_for_blank_query()
        {
            Assert.Empty(_service.FullTextSearch(""));
            Assert.Empty(_service.FullTextSearch("   "));
            Assert.Empty(_service.FullTextSearch(null));
        }

        [Fact]
        public void Snippet_window_is_centered_on_first_match()
        {
            var prefix = new string('x', 200);
            var suffix = new string('y', 200);
            var (_, a) = Seed("a.txt", prefix + "приказ" + suffix);
            _service.IndexAttachment(a.Id);

            var hits = _service.FullTextSearch("приказ");
            Assert.Single(hits);
            Assert.Contains("приказ", hits[0].Snippet);
            // Окно ±80 — общий размер не должен превышать ~170 символов.
            Assert.InRange(hits[0].Snippet.Length, 60, 200);
        }
    }
}
