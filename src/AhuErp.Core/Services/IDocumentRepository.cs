using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Абстракция доступа к документам. CRUD-операции простые и синхронные —
    /// UI-клиент Phase 2 использует их напрямую из ViewModel.
    /// </summary>
    public interface IDocumentRepository
    {
        IReadOnlyList<Document> ListByType(DocumentType type);
        IReadOnlyList<ArchiveRequest> ListArchiveRequests();
        IReadOnlyList<ItTicket> ListItTickets();

        /// <summary>Документы-основания для списания ТМЦ: внутренние распоряжения и IT-заявки.</summary>
        IReadOnlyList<Document> ListInventoryEligibleDocuments();

        Document GetById(int id);
        void Add(Document document);
        void Update(Document document);
        void Remove(int id);

        /// <summary>
        /// Универсальный поиск документов по фильтру (используется в журналах
        /// регистрации и глобальном поиске Ctrl+F). Сортировка стабильная:
        /// по <see cref="Document.RegistrationDate"/> DESC (или CreationDate),
        /// затем по Id.
        /// </summary>
        IReadOnlyList<Document> Search(DocumentSearchFilter filter);
    }
}
