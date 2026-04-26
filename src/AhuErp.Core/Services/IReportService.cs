using System;
using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Сервис выгрузки табличных отчётов и формальных документов. Абстрагирует
    /// ViewModel от ClosedXML / OpenXML, чтобы UI оставался свободным от
    /// зависимостей на форматы файлов.
    /// </summary>
    public interface IReportService
    {
        /// <summary>
        /// Формирует XLSX-файл со списком всех позиций ТМЦ (ID, Наименование,
        /// Категория, Остаток) с отформатированной шапкой и автошириной колонок.
        /// </summary>
        /// <param name="filePath">Целевой путь для записи файла.</param>
        void ExportInventoryToExcel(string filePath);

        /// <summary>
        /// Генерирует Word-справку (DOCX) по архивному запросу: подставляет
        /// номер, дату, тему, статусы сканов и формальный текст ответа.
        /// </summary>
        /// <param name="archiveRequestId">Идентификатор <see cref="Models.ArchiveRequest"/>.</param>
        /// <param name="filePath">Целевой путь для записи файла.</param>
        void GenerateArchiveCertificate(int archiveRequestId, string filePath);

        /// <summary>
        /// Журнал регистрации документов за период с группировкой по дате
        /// и шапкой по требованиям делопроизводства (Рег. №, Дата, Вид,
        /// Заголовок, Корреспондент, Исполнитель, Срок, Статус).
        /// </summary>
        /// <param name="documents">Снимок документов журнала (из <see cref="IDocumentRepository.Search"/>).</param>
        /// <param name="title">Название журнала, например «Журнал входящих».</param>
        /// <param name="filePath">Целевой путь.</param>
        void ExportRegistrationJournal(IEnumerable<Document> documents, string title, string filePath);

        /// <summary>
        /// Отчёт «Исполнительская дисциплина» за период: по сотрудникам всего
        /// поручений / выполнено / просрочено / процент дисциплины.
        /// </summary>
        void ExportExecutionDisciplineReport(DateTime from, DateTime to, string filePath);

        /// <summary>
        /// Отчёт «Объём документооборота»: число документов по направлениям
        /// и видам за указанный период. По строкам — вид документа,
        /// по колонкам — направление.
        /// </summary>
        void ExportDocumentVolumeReport(DateTime from, DateTime to, string filePath);

        /// <summary>
        /// Отчёт «Просроченные поручения»: список всех активных просроченных
        /// поручений с автором, исполнителем, сроком и днями просрочки.
        /// </summary>
        void ExportOverdueTasksReport(string filePath);

        /// <summary>
        /// Отчёт «Аналитика по номенклатуре дел»: количество документов
        /// в каждом деле за период, с указанием срока хранения и отдела.
        /// </summary>
        void ExportNomenclatureAnalyticsReport(DateTime from, DateTime to, string filePath);
    }
}
