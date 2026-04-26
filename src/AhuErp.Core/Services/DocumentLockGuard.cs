using System;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Phase 8 — снимок «иммутабельных при блокировке» полей документа.
    /// Когда <see cref="Document.IsLocked"/> = <c>true</c>, репозиторий
    /// сравнивает текущий документ с его снимком и бросает исключение, если
    /// изменилось хоть одно поле, кроме <see cref="Document.Status"/>,
    /// <see cref="Document.AssignedEmployeeId"/>,
    /// <see cref="Document.AccessLevel"/>,
    /// <see cref="Document.IsLocked"/>,
    /// <see cref="Document.CurrentVersionAttachmentId"/>.
    /// </summary>
    public sealed class DocumentLockSnapshot
    {
        public bool IsLocked { get; private set; }
        public string Signature { get; private set; }

        public static DocumentLockSnapshot Of(Document d)
        {
            if (d == null) throw new ArgumentNullException(nameof(d));
            return new DocumentLockSnapshot
            {
                IsLocked = d.IsLocked,
                Signature = BuildSignature(d),
            };
        }

        public static string BuildSignature(Document d)
            => string.Join("|", new object[]
            {
                d.Type, d.Direction,
                d.Title ?? string.Empty,
                d.Summary ?? string.Empty,
                d.Correspondent ?? string.Empty,
                d.IncomingNumber ?? string.Empty,
                d.IncomingDate?.Ticks ?? 0,
                d.RegistrationNumber ?? string.Empty,
                d.RegistrationDate?.Ticks ?? 0,
                d.DocumentTypeRefId ?? 0,
                d.NomenclatureCaseId ?? 0,
                d.AuthorId ?? 0,
                d.CreationDate.Ticks,
                d.Deadline.Ticks,
                d.BasisDocumentId ?? 0,
                d.ApprovalStatus,
            });

        public void Enforce(Document current)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            if (!IsLocked) return;
            var newSig = BuildSignature(current);
            if (!string.Equals(newSig, Signature, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Документ заблокирован подписью: разрешено менять только " +
                    "статус, исполнителя и гриф доступа.");
            }
        }
    }
}
