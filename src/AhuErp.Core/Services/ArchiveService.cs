using System;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Реализация <see cref="IArchiveService"/>. Содержит только бизнес-логику,
    /// от EF-инфраструктуры не зависит.
    /// </summary>
    public class ArchiveService : IArchiveService
    {
        public ArchiveRequest CreateRequest(string title, DateTime creationDate, int? assignedEmployeeId = null)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Заголовок архивного запроса не может быть пустым.", nameof(title));
            }

            var request = new ArchiveRequest
            {
                Title = title,
                Status = DocumentStatus.New,
                AssignedEmployeeId = assignedEmployeeId
            };
            request.InitializeDeadline(creationDate);
            return request;
        }

        public void CompleteRequest(ArchiveRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!request.CanCompleteRequest())
            {
                throw new InvalidOperationException(
                    "Архивный запрос не может быть закрыт: требуются скан-копии паспорта и трудовой книжки.");
            }

            request.Status = DocumentStatus.Completed;
        }
    }
}
