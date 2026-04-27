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
    /// Phase 8 — покрытие <see cref="SignatureService"/>:
    /// happy-path ПЭП/КЭП, дедупликация, блокировка документа,
    /// guard на правке заблокированного, обнаружение подмены файла,
    /// автоотзыв ПЭП/НЭП при загрузке новой версии вложения.
    /// </summary>
    public class SignatureServiceTests
    {
        private readonly InMemoryDocumentRepository _docs = new InMemoryDocumentRepository();
        private readonly InMemoryAttachmentRepository _attRepo = new InMemoryAttachmentRepository();
        private readonly InMemoryFileStorageService _storage = new InMemoryFileStorageService();
        private readonly InMemoryAuditLogRepository _auditRepo = new InMemoryAuditLogRepository();
        private readonly InMemorySignatureRepository _sigRepo = new InMemorySignatureRepository();
        private readonly InMemoryEmployeeRepository _employees;
        private readonly AuditService _audit;
        private readonly SignatureService _signatures;
        private readonly AttachmentService _attachments;
        private readonly Employee _author;
        private readonly Employee _signer;

        public SignatureServiceTests()
        {
            _author = new Employee { Id = 1, FullName = "Иванов И.И.", Role = EmployeeRole.Manager, PasswordHash = "hash-1" };
            _signer = new Employee { Id = 2, FullName = "Петров П.П.", Role = EmployeeRole.Manager, PasswordHash = "hash-2" };
            _employees = new InMemoryEmployeeRepository(new[] { _author, _signer });
            _audit = new AuditService(_auditRepo);
            _signatures = new SignatureService(
                _sigRepo, _docs, _attRepo, _employees, _audit,
                hmac: new HmacCryptoProvider(),
                qualified: new HmacCryptoProvider()); // в тестах КЭП эмулируем тем же HMAC, чтобы Verify работал.
            _attachments = new AttachmentService(_attRepo, _docs, _storage, _audit, _signatures);
        }

        private Document CreateDoc(string title = "Служебная записка")
        {
            var doc = new Document
            {
                Title = title,
                Type = DocumentType.Internal,
                CreationDate = DateTime.Now,
                Deadline = DateTime.Now.AddDays(7),
                AuthorId = _author.Id,
                AccessLevel = DocumentAccessLevel.Public,
            };
            _docs.Add(doc);
            return doc;
        }

        private DocumentAttachment UploadAttachment(int docId, string content = "payload")
            => _attachments.Upload(docId, new MemoryStream(Encoding.UTF8.GetBytes(content)),
                "draft.pdf", uploadedById: _signer.Id);

        // ---------------- happy path -------------------------------------

        [Fact]
        public void Sign_simple_creates_signature_and_audit_and_verifies()
        {
            var doc = CreateDoc();
            var att = UploadAttachment(doc.Id);

            var sig = _signatures.Sign(doc.Id, att.Id, _signer.Id, SignatureKind.Simple,
                reason: "Согласовано");

            Assert.NotNull(sig);
            Assert.False(sig.IsRevoked);
            Assert.Equal(SignatureKind.Simple, sig.Kind);
            Assert.False(string.IsNullOrEmpty(sig.SignedHash));
            Assert.False(string.IsNullOrEmpty(sig.SignatureBlobBase64));
            Assert.True(_signatures.Verify(sig.Id));

            var audited = _auditRepo.Query(new AuditQueryFilter { ActionType = AuditActionType.SignatureAdded });
            Assert.Single(audited);
        }

        [Fact]
        public void Sign_qualified_locks_document_and_records_lock_audit()
        {
            var doc = CreateDoc();
            var att = UploadAttachment(doc.Id);

            _signatures.Sign(doc.Id, att.Id, _signer.Id, SignatureKind.Qualified,
                reason: "КЭП", certificateThumbprint: "ABC123");

            var stored = _docs.GetById(doc.Id);
            Assert.True(stored.IsLocked);
            Assert.Equal(att.Id, stored.CurrentVersionAttachmentId);

            var locked = _auditRepo.Query(new AuditQueryFilter { ActionType = AuditActionType.DocumentLocked });
            Assert.Single(locked);
        }

        [Fact]
        public void Sign_simple_does_not_lock_document()
        {
            var doc = CreateDoc();
            _signatures.Sign(doc.Id, attachmentId: null, _signer.Id, SignatureKind.Simple);
            Assert.False(_docs.GetById(doc.Id).IsLocked);
        }

        // ---------------- guards & validation ----------------------------

        [Fact]
        public void Sign_throws_on_double_sign_same_signer_kind_attachment()
        {
            var doc = CreateDoc();
            var att = UploadAttachment(doc.Id);
            _signatures.Sign(doc.Id, att.Id, _signer.Id, SignatureKind.Simple);

            Assert.Throws<InvalidOperationException>(() =>
                _signatures.Sign(doc.Id, att.Id, _signer.Id, SignatureKind.Simple));
        }

        [Fact]
        public void Sign_qualified_requires_thumbprint()
        {
            var doc = CreateDoc();
            Assert.Throws<ArgumentException>(() =>
                _signatures.Sign(doc.Id, attachmentId: null, _signer.Id, SignatureKind.Qualified,
                    certificateThumbprint: null));
        }

        [Fact]
        public void Sign_throws_when_attachment_belongs_to_other_document()
        {
            var docA = CreateDoc("A");
            var docB = CreateDoc("B");
            var attA = UploadAttachment(docA.Id);

            Assert.Throws<InvalidOperationException>(() =>
                _signatures.Sign(docB.Id, attA.Id, _signer.Id, SignatureKind.Simple));
        }

        [Fact]
        public void Sign_throws_for_unknown_document_or_signer()
        {
            Assert.Throws<InvalidOperationException>(() =>
                _signatures.Sign(999, null, _signer.Id, SignatureKind.Simple));
            var doc = CreateDoc();
            Assert.Throws<InvalidOperationException>(() =>
                _signatures.Sign(doc.Id, null, signerId: 999, kind: SignatureKind.Simple));
        }

        // ---------------- locked-document guard --------------------------

        [Fact]
        public void Locked_document_blocks_title_change()
        {
            var doc = CreateDoc();
            var att = UploadAttachment(doc.Id);
            _signatures.Sign(doc.Id, att.Id, _signer.Id, SignatureKind.Qualified,
                certificateThumbprint: "CERT");

            var loaded = _docs.GetById(doc.Id);
            loaded.Title = "Подменили заголовок";
            Assert.Throws<InvalidOperationException>(() => _docs.Update(loaded));
        }

        [Fact]
        public void Locked_document_allows_status_assignee_access_changes()
        {
            var doc = CreateDoc();
            var att = UploadAttachment(doc.Id);
            _signatures.Sign(doc.Id, att.Id, _signer.Id, SignatureKind.Qualified,
                certificateThumbprint: "CERT");

            var loaded = _docs.GetById(doc.Id);
            loaded.Status = DocumentStatus.InProgress;
            loaded.AssignedEmployeeId = _signer.Id;
            loaded.AccessLevel = DocumentAccessLevel.Internal;
            _docs.Update(loaded); // ОК

            var fresh = _docs.GetById(doc.Id);
            Assert.Equal(DocumentStatus.InProgress, fresh.Status);
            Assert.Equal(_signer.Id, fresh.AssignedEmployeeId);
            Assert.Equal(DocumentAccessLevel.Internal, fresh.AccessLevel);
        }

        [Fact]
        public void Verify_returns_true_after_legitimate_access_level_change()
        {
            // Регрессия по Devin Review #4 (RED): AccessLevel был в подписанном
            // payload, но whitelist лок-гарда позволяет его менять. Verify
            // обязан оставаться true после смены грифа.
            var doc = CreateDoc();
            var att = UploadAttachment(doc.Id);
            var sig = _signatures.Sign(doc.Id, att.Id, _signer.Id, SignatureKind.Qualified,
                certificateThumbprint: "CERT");
            Assert.True(_signatures.Verify(sig.Id));

            var loaded = _docs.GetById(doc.Id);
            loaded.AccessLevel = DocumentAccessLevel.Internal;
            _docs.Update(loaded);

            Assert.True(_signatures.Verify(sig.Id));
        }

        [Fact]
        public void Locked_document_allows_approval_status_changes()
        {
            // Регрессия по Devin Review #3 (RED): ApprovalStatus был ошибочно
            // в immutable-списке. Это блокировало StartApproval/ApplyDecision/
            // WorkflowService.OnApprovalRouteCompleted на КЭП-документах.
            var doc = CreateDoc();
            var att = UploadAttachment(doc.Id);
            _signatures.Sign(doc.Id, att.Id, _signer.Id, SignatureKind.Qualified,
                certificateThumbprint: "CERT");

            var loaded = _docs.GetById(doc.Id);
            loaded.ApprovalStatus = ApprovalRouteStatus.InProgress;
            _docs.Update(loaded);

            var fresh = _docs.GetById(doc.Id);
            Assert.Equal(ApprovalRouteStatus.InProgress, fresh.ApprovalStatus);
        }

        // ---------------- file substitution ------------------------------

        [Fact]
        public void Verify_returns_false_when_attachment_hash_changes()
        {
            var doc = CreateDoc();
            var att = UploadAttachment(doc.Id, "original");
            var sig = _signatures.Sign(doc.Id, att.Id, _signer.Id, SignatureKind.Simple);
            Assert.True(_signatures.Verify(sig.Id));

            // Подменяем «вложение» — меняем хэш файла.
            att.Hash = "tampered-hash";
            _attRepo.Update(att);

            Assert.False(_signatures.Verify(sig.Id));
        }

        // ---------------- revoke -----------------------------------------

        [Fact]
        public void Revoke_marks_signature_revoked_and_writes_audit()
        {
            var doc = CreateDoc();
            var sig = _signatures.Sign(doc.Id, null, _signer.Id, SignatureKind.Simple);
            _signatures.Revoke(sig.Id, _signer.Id, "Ошибся версией");

            var stored = _sigRepo.Get(sig.Id);
            Assert.True(stored.IsRevoked);
            Assert.NotNull(stored.RevokedAt);
            Assert.False(_signatures.Verify(sig.Id));

            var audited = _auditRepo.Query(new AuditQueryFilter { ActionType = AuditActionType.SignatureRevoked });
            Assert.Single(audited);
        }

        [Fact]
        public void RevokeAllNonQualified_revokes_simple_keeps_qualified()
        {
            var doc = CreateDoc();
            var att = UploadAttachment(doc.Id);
            var simple = _signatures.Sign(doc.Id, att.Id, _signer.Id, SignatureKind.Simple);
            var enhanced = _signatures.Sign(doc.Id, att.Id, _author.Id, SignatureKind.Enhanced);
            var qualified = _signatures.Sign(doc.Id, att.Id, _author.Id, SignatureKind.Qualified,
                certificateThumbprint: "CERT");

            int count = _signatures.RevokeAllNonQualified(doc.Id, _signer.Id, "новая версия");
            Assert.Equal(2, count);
            Assert.True(_sigRepo.Get(simple.Id).IsRevoked);
            Assert.True(_sigRepo.Get(enhanced.Id).IsRevoked);
            Assert.False(_sigRepo.Get(qualified.Id).IsRevoked);
        }

        [Fact]
        public void AttachmentService_AddVersion_revokes_non_qualified_signatures()
        {
            var doc = CreateDoc();
            var v1 = UploadAttachment(doc.Id, "v1-content");
            var sigSimple = _signatures.Sign(doc.Id, v1.Id, _signer.Id, SignatureKind.Simple);
            var sigQualified = _signatures.Sign(doc.Id, v1.Id, _author.Id, SignatureKind.Qualified,
                certificateThumbprint: "CERT");

            // Загружаем новую версию вложения — должно отозвать ПЭП, оставить КЭП.
            _attachments.AddVersion(v1.AttachmentGroupId,
                new MemoryStream(Encoding.UTF8.GetBytes("v2-content")),
                "draft.pdf", uploadedById: _signer.Id);

            Assert.True(_sigRepo.Get(sigSimple.Id).IsRevoked);
            Assert.False(_sigRepo.Get(sigQualified.Id).IsRevoked);
        }

        // ---------------- listing / IsDocumentSigned ---------------------

        [Fact]
        public void IsDocumentSigned_respects_minimum_kind_and_revocation()
        {
            var doc = CreateDoc();
            Assert.False(_signatures.IsDocumentSigned(doc.Id));

            var sig = _signatures.Sign(doc.Id, null, _signer.Id, SignatureKind.Simple);
            Assert.True(_signatures.IsDocumentSigned(doc.Id));
            Assert.False(_signatures.IsDocumentSigned(doc.Id, SignatureKind.Qualified));

            _signatures.Revoke(sig.Id, _signer.Id, "x");
            Assert.False(_signatures.IsDocumentSigned(doc.Id));
        }

        [Fact]
        public void ListByDocument_returns_all_including_revoked()
        {
            var doc = CreateDoc();
            var s1 = _signatures.Sign(doc.Id, null, _signer.Id, SignatureKind.Simple);
            var s2 = _signatures.Sign(doc.Id, null, _author.Id, SignatureKind.Enhanced);
            _signatures.Revoke(s1.Id, _signer.Id, "x");

            var list = _signatures.ListByDocument(doc.Id);
            Assert.Equal(2, list.Count);
            Assert.Contains(list, s => s.Id == s1.Id && s.IsRevoked);
            Assert.Contains(list, s => s.Id == s2.Id && !s.IsRevoked);
        }
    }
}
