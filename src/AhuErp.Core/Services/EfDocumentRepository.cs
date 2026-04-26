using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using AhuErp.Core.Data;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// EF6-реализация <see cref="IDocumentRepository"/> поверх <see cref="AhuDbContext"/>.
    /// Использует длинноживущий контекст (singleton в DI), все обращения — с UI-потока
    /// (см. <see cref="ViewModels.DashboardViewModel"/>: снимок данных перед Task.Run).
    /// TPH-наследники (<see cref="ArchiveRequest"/>, <see cref="ItTicket"/>) живут в той
    /// же таблице <c>Documents</c> и различаются дискриминатором <c>DocumentKind</c>.
    /// </summary>
    public sealed class EfDocumentRepository : IDocumentRepository
    {
        private readonly AhuDbContext _ctx;

        public EfDocumentRepository(AhuDbContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        }

        public IReadOnlyList<Document> ListByType(DocumentType type)
        {
            return _ctx.Documents
                .Where(d => d.Type == type
                            && !(d is ArchiveRequest)
                            && !(d is ItTicket))
                .ToList()
                .AsReadOnly();
        }

        public IReadOnlyList<ArchiveRequest> ListArchiveRequests()
        {
            return _ctx.Documents.OfType<ArchiveRequest>().ToList().AsReadOnly();
        }

        public IReadOnlyList<ItTicket> ListItTickets()
        {
            return _ctx.Documents.OfType<ItTicket>().ToList().AsReadOnly();
        }

        public IReadOnlyList<Document> ListInventoryEligibleDocuments()
        {
            return _ctx.Documents
                .Where(d => d.Type == DocumentType.Internal
                            || d.Type == DocumentType.It
                            || d is ItTicket)
                .ToList()
                .AsReadOnly();
        }

        public Document GetById(int id) => _ctx.Documents.Find(id);

        public void Add(Document document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            _ctx.Documents.Add(document);
            _ctx.SaveChanges();
        }

        public void Update(Document document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            // Если сущность уже в трекере (типичный сценарий — её достали через Find/List
            // и тут же редактируют) — EF6 уже знает об изменениях. Если detached
            // (после рестарта контекста или при ручной сборке) — присоединяем явно.
            if (_ctx.Entry(document).State == EntityState.Detached)
            {
                _ctx.Documents.Attach(document);
                _ctx.Entry(document).State = EntityState.Modified;
            }
            _ctx.SaveChanges();
        }

        public void Remove(int id)
        {
            var doc = _ctx.Documents.Find(id);
            if (doc == null) return;
            _ctx.Documents.Remove(doc);
            _ctx.SaveChanges();
        }

        public IReadOnlyList<Document> Search(DocumentSearchFilter filter)
        {
            if (filter == null) filter = new DocumentSearchFilter();
            IQueryable<Document> q = _ctx.Documents;

            if (filter.Direction.HasValue)
            {
                var dir = filter.Direction.Value;
                q = q.Where(d => d.Direction == dir);
            }
            if (filter.StatusIn != null && filter.StatusIn.Length > 0)
            {
                var statuses = filter.StatusIn;
                q = q.Where(d => statuses.Contains(d.Status));
            }
            else if (filter.Status.HasValue)
            {
                var st = filter.Status.Value;
                q = q.Where(d => d.Status == st);
            }
            if (filter.NomenclatureCaseId.HasValue)
            {
                var caseId = filter.NomenclatureCaseId.Value;
                q = q.Where(d => d.NomenclatureCaseId == caseId);
            }
            if (filter.DocumentTypeRefId.HasValue)
            {
                var typeId = filter.DocumentTypeRefId.Value;
                q = q.Where(d => d.DocumentTypeRefId == typeId);
            }
            if (filter.AssignedEmployeeId.HasValue)
            {
                var eid = filter.AssignedEmployeeId.Value;
                q = q.Where(d => d.AssignedEmployeeId == eid);
            }
            if (filter.RegisteredOnly)
            {
                q = q.Where(d => d.RegistrationNumber != null && d.RegistrationDate.HasValue);
            }
            if (filter.From.HasValue)
            {
                var from = filter.From.Value;
                q = q.Where(d => (d.RegistrationDate ?? d.CreationDate) >= from);
            }
            if (filter.To.HasValue)
            {
                var to = filter.To.Value;
                q = q.Where(d => (d.RegistrationDate ?? d.CreationDate) <= to);
            }
            if (!string.IsNullOrWhiteSpace(filter.Correspondent))
            {
                var c = filter.Correspondent.Trim();
                q = q.Where(d => d.Correspondent != null && d.Correspondent.Contains(c));
            }
            if (!string.IsNullOrWhiteSpace(filter.Text))
            {
                var t = filter.Text.Trim();
                q = q.Where(d =>
                       (d.Title != null && d.Title.Contains(t))
                    || (d.Summary != null && d.Summary.Contains(t))
                    || (d.RegistrationNumber != null && d.RegistrationNumber.Contains(t))
                    || (d.Correspondent != null && d.Correspondent.Contains(t))
                    || (d.IncomingNumber != null && d.IncomingNumber.Contains(t)));
            }

            var list = q
                .OrderByDescending(d => d.RegistrationDate ?? d.CreationDate)
                .ThenByDescending(d => d.Id)
                .ToList();

            // Просрочка вычисляется на стороне клиента (используем DateTime.Now, что не транслируется в SQL).
            if (filter.OverdueOnly)
            {
                var now = DateTime.Now;
                list = list.Where(d => d.IsOverdue(now)).ToList();
            }
            return list.AsReadOnly();
        }
    }
}
