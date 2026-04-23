using System;
using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Сервис расчёта сводных показателей (KPI) для дашборда руководителя.
    /// </summary>
    public interface IDashboardService
    {
        /// <summary>
        /// Количество просроченных документов на момент <paramref name="now"/>.
        /// </summary>
        int CountOverdue(IEnumerable<Document> documents, DateTime now);

        /// <summary>
        /// Количество документов, срок которых истекает менее чем через <paramref name="daysThreshold"/> суток.
        /// </summary>
        int CountDueSoon(IEnumerable<Document> documents, DateTime now, int daysThreshold = 3);
    }
}
