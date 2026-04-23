using System;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Сервис обработки архивных запросов.
    /// </summary>
    public interface IArchiveService
    {
        /// <summary>
        /// Создаёт новый архивный запрос с регламентным сроком в 30 дней.
        /// </summary>
        ArchiveRequest CreateRequest(string title, DateTime creationDate, int? assignedEmployeeId = null);

        /// <summary>
        /// Переводит архивный запрос в статус <see cref="DocumentStatus.Completed"/>,
        /// если предоставлен полный комплект скан-копий.
        /// </summary>
        /// <exception cref="InvalidOperationException">Пакет документов неполон.</exception>
        void CompleteRequest(ArchiveRequest request);
    }
}
