using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AhuErp.Core.Models;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Реализация <see cref="IReportService"/>. Использует ClosedXML для XLSX
    /// и DocumentFormat.OpenXml для DOCX — оба работают на чистом .NET без
    /// установленного MS Office.
    /// </summary>
    public sealed class ReportService : IReportService
    {
        private readonly IInventoryRepository _inventory;
        private readonly IDocumentRepository _documents;
        private readonly ITaskService _tasks;
        private readonly ITaskRepository _taskRepo;
        private readonly INomenclatureRepository _nomenclature;

        public ReportService(IInventoryRepository inventory, IDocumentRepository documents)
            : this(inventory, documents, null, null, null)
        {
        }

        /// <summary>
        /// Полная DI-перегрузка для отчётов СЭД (исполнительская дисциплина,
        /// объём документооборота, просрочка, аналитика по номенклатуре).
        /// Старая 2-аргументная перегрузка сохранена ради обратной совместимости
        /// с тестами Phase 4.
        /// </summary>
        public ReportService(
            IInventoryRepository inventory,
            IDocumentRepository documents,
            ITaskService tasks,
            ITaskRepository taskRepo,
            INomenclatureRepository nomenclature)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _tasks = tasks;
            _taskRepo = taskRepo;
            _nomenclature = nomenclature;
        }

        public void ExportInventoryToExcel(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу обязателен.", nameof(filePath));

            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.Worksheets.Add("Склад ТМЦ");

                sheet.Cell(1, 1).Value = "№";
                sheet.Cell(1, 2).Value = "Наименование";
                sheet.Cell(1, 3).Value = "Категория";
                sheet.Cell(1, 4).Value = "Остаток";

                var header = sheet.Range(1, 1, 1, 4);
                header.Style.Font.Bold = true;
                header.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
                header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                header.Style.Border.BottomBorder = XLBorderStyleValues.Medium;

                var row = 2;
                foreach (var item in _inventory.ListItems().OrderBy(i => i.Name))
                {
                    sheet.Cell(row, 1).Value = item.Id;
                    sheet.Cell(row, 2).Value = item.Name;
                    sheet.Cell(row, 3).Value = FormatCategory(item.Category);
                    sheet.Cell(row, 4).Value = item.TotalQuantity;
                    row++;
                }

                sheet.Columns().AdjustToContents();

                workbook.SaveAs(filePath);
            }
        }

        public void GenerateArchiveCertificate(int archiveRequestId, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу обязателен.", nameof(filePath));

            var document = _documents.GetById(archiveRequestId) as ArchiveRequest
                ?? throw new InvalidOperationException(
                    $"Архивный запрос #{archiveRequestId} не найден.");

            using (var doc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document))
            {
                var main = doc.AddMainDocumentPart();
                main.Document = new W.Document();
                var body = main.Document.AppendChild(new W.Body());

                body.AppendChild(Paragraph(OrganizationProfile.FullName));
                body.AppendChild(Paragraph(OrganizationProfile.ArchiveDepartmentName));
                body.AppendChild(Paragraph(OrganizationProfile.ArchiveAddress));
                body.AppendChild(Paragraph($"Телефон: {OrganizationProfile.ArchivePhone}; e-mail: {OrganizationProfile.ArchiveEmail}"));
                body.AppendChild(Paragraph(string.Empty));
                body.AppendChild(Heading("АРХИВНАЯ СПРАВКА"));
                body.AppendChild(Paragraph($"по архивному запросу №{document.Id} от {document.CreationDate:dd.MM.yyyy}"));
                body.AppendChild(Paragraph(string.Empty));
                body.AppendChild(Paragraph($"Вид запроса: {FormatArchiveRequestKind(document.RequestKind)}"));
                body.AppendChild(Paragraph($"Тема запроса: {document.Title}"));
                body.AppendChild(Paragraph($"Срок исполнения: {document.Deadline:dd.MM.yyyy}"));
                body.AppendChild(Paragraph(string.Empty));

                var passport = document.HasPassportScan ? "приложен" : "не приложен";
                var workBook = document.HasWorkBookScan ? "приложена" : "не приложена";
                body.AppendChild(Paragraph($"Скан паспорта: {passport}."));
                body.AppendChild(Paragraph($"Скан трудовой книжки: {workBook}."));
                body.AppendChild(Paragraph(string.Empty));

                if (document.HasPassportScan && document.HasWorkBookScan)
                {
                    body.AppendChild(Paragraph(
                        "Настоящим подтверждается, что документы представлены в полном объёме. " +
                        "Архивная справка, выписка или копия подготовлена для выдачи заявителю."));
                }
                else
                {
                    body.AppendChild(Paragraph(
                        "Для выдачи архивной справки необходимо дополнительно представить " +
                        "отсутствующие документы, после чего запрос будет обработан повторно."));
                }

                body.AppendChild(Paragraph(string.Empty));
                body.AppendChild(Paragraph($"Начальник архивного отдела _________________________ {OrganizationProfile.ArchiveHeadShortName}"));
                body.AppendChild(Paragraph($"Дата оформления: {DateTime.Now:dd.MM.yyyy}"));

                main.Document.Save();
            }
        }

        public void ExportRegistrationJournal(IEnumerable<Document> documents, string title, string filePath)
        {
            if (documents == null) throw new ArgumentNullException(nameof(documents));
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу обязателен.", nameof(filePath));

            var rows = documents.ToList();
            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.Worksheets.Add(SafeSheetName(string.IsNullOrWhiteSpace(title) ? "Журнал" : title));

                sheet.Cell(1, 1).Value = string.IsNullOrWhiteSpace(title) ? "Журнал регистрации" : title;
                sheet.Range(1, 1, 1, 9).Merge().Style
                    .Font.SetBold(true).Font.SetFontSize(14)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                sheet.Cell(2, 1).Value = $"Сформировано: {DateTime.Now:dd.MM.yyyy HH:mm}";
                sheet.Range(2, 1, 2, 9).Merge();

                int header = 4;
                sheet.Cell(header, 1).Value = "Рег. №";
                sheet.Cell(header, 2).Value = "Дата рег.";
                sheet.Cell(header, 3).Value = "Направление";
                sheet.Cell(header, 4).Value = "Вид";
                sheet.Cell(header, 5).Value = "Заголовок";
                sheet.Cell(header, 6).Value = "Корреспондент";
                sheet.Cell(header, 7).Value = "Исполнитель";
                sheet.Cell(header, 8).Value = "Срок";
                sheet.Cell(header, 9).Value = "Статус";

                var hdr = sheet.Range(header, 1, header, 9);
                hdr.Style.Font.Bold = true;
                hdr.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
                hdr.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                hdr.Style.Border.BottomBorder = XLBorderStyleValues.Medium;

                int row = header + 1;
                foreach (var d in rows)
                {
                    sheet.Cell(row, 1).Value = d.RegistrationNumber ?? "—";
                    sheet.Cell(row, 2).Value = d.RegistrationDate?.ToString("dd.MM.yyyy") ?? "—";
                    sheet.Cell(row, 3).Value = FormatDirection(d.Direction);
                    sheet.Cell(row, 4).Value = d.DocumentTypeRef?.Name ?? FormatDocumentType(d.Type);
                    sheet.Cell(row, 5).Value = d.Title ?? string.Empty;
                    sheet.Cell(row, 6).Value = d.Correspondent ?? string.Empty;
                    sheet.Cell(row, 7).Value = d.AssignedEmployee?.FullName ?? string.Empty;
                    sheet.Cell(row, 8).Value = d.Deadline.ToString("dd.MM.yyyy");
                    sheet.Cell(row, 9).Value = FormatDocumentStatus(d.Status);
                    row++;
                }

                sheet.Columns().AdjustToContents();
                sheet.Column(5).Width = Math.Min(60, sheet.Column(5).Width);
                workbook.SaveAs(filePath);
            }
        }

        public void ExportExecutionDisciplineReport(DateTime from, DateTime to, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу обязателен.", nameof(filePath));
            if (_tasks == null)
                throw new InvalidOperationException("ReportService не настроен для отчётов СЭД (нет ITaskService).");

            var report = _tasks.BuildDisciplineReport(from, to);

            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.Worksheets.Add("Дисциплина");
                sheet.Cell(1, 1).Value = "Исполнительская дисциплина";
                sheet.Range(1, 1, 1, 6).Merge().Style.Font.SetBold(true).Font.SetFontSize(14)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                sheet.Cell(2, 1).Value = $"Период: {from:dd.MM.yyyy} — {to:dd.MM.yyyy}";
                sheet.Range(2, 1, 2, 6).Merge();

                sheet.Cell(4, 1).Value = "Всего поручений";
                sheet.Cell(4, 2).Value = report.TotalTasks;
                sheet.Cell(5, 1).Value = "В срок";
                sheet.Cell(5, 2).Value = report.CompletedOnTime;
                sheet.Cell(6, 1).Value = "С нарушением срока";
                sheet.Cell(6, 2).Value = report.CompletedLate;
                sheet.Cell(7, 1).Value = "Просрочено (открытые)";
                sheet.Cell(7, 2).Value = report.Overdue;
                sheet.Cell(8, 1).Value = "В работе";
                sheet.Cell(8, 2).Value = report.InProgress;

                int hdr = 10;
                sheet.Cell(hdr, 1).Value = "Исполнитель";
                sheet.Cell(hdr, 2).Value = "Всего";
                sheet.Cell(hdr, 3).Value = "В срок";
                sheet.Cell(hdr, 4).Value = "Опоздание";
                sheet.Cell(hdr, 5).Value = "Просрочено";
                sheet.Cell(hdr, 6).Value = "% дисциплины";
                var headerRange = sheet.Range(hdr, 1, hdr, 6);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
                headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Medium;

                int row = hdr + 1;
                foreach (var r in report.ByExecutor)
                {
                    double pct = r.Total == 0 ? 0.0 : 100.0 * r.CompletedOnTime / r.Total;
                    sheet.Cell(row, 1).Value = r.ExecutorName;
                    sheet.Cell(row, 2).Value = r.Total;
                    sheet.Cell(row, 3).Value = r.CompletedOnTime;
                    sheet.Cell(row, 4).Value = r.CompletedLate;
                    sheet.Cell(row, 5).Value = r.Overdue;
                    sheet.Cell(row, 6).Value = Math.Round(pct, 1);
                    row++;
                }

                sheet.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            }
        }

        public void ExportDocumentVolumeReport(DateTime from, DateTime to, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу обязателен.", nameof(filePath));

            // `to` обычно приходит как полночь (DatePicker), поэтому
            // расширяем до конца дня — иначе документы за последний день
            // периода не попадут в выборку (`<= to` в Search).
            var toInclusive = ExtendToEndOfDay(to);
            var docs = _documents.Search(new DocumentSearchFilter { From = from, To = toInclusive });

            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.Worksheets.Add("Объём");
                sheet.Cell(1, 1).Value = "Объём документооборота";
                sheet.Range(1, 1, 1, 6).Merge().Style.Font.SetBold(true).Font.SetFontSize(14)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                sheet.Cell(2, 1).Value = $"Период: {from:dd.MM.yyyy} — {to:dd.MM.yyyy}; всего: {docs.Count}";
                sheet.Range(2, 1, 2, 6).Merge();

                int hdr = 4;
                sheet.Cell(hdr, 1).Value = "Вид документа";
                sheet.Cell(hdr, 2).Value = "Входящих";
                sheet.Cell(hdr, 3).Value = "Исходящих";
                sheet.Cell(hdr, 4).Value = "Внутренних";
                sheet.Cell(hdr, 5).Value = "Распорядительных";
                sheet.Cell(hdr, 6).Value = "Итого";
                var headerRange = sheet.Range(hdr, 1, hdr, 6);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
                headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Medium;

                var grouped = docs
                    .GroupBy(d => d.DocumentTypeRef?.Name ?? FormatDocumentType(d.Type))
                    .OrderBy(g => g.Key)
                    .ToList();

                int row = hdr + 1;
                int totalIn = 0, totalOut = 0, totalInt = 0, totalDir = 0;
                foreach (var g in grouped)
                {
                    int incoming = g.Count(d => d.Direction == DocumentDirection.Incoming);
                    int outgoing = g.Count(d => d.Direction == DocumentDirection.Outgoing);
                    int internalCnt = g.Count(d => d.Direction == DocumentDirection.Internal);
                    int directiveCnt = g.Count(d => d.Direction == DocumentDirection.Directive);
                    sheet.Cell(row, 1).Value = g.Key;
                    sheet.Cell(row, 2).Value = incoming;
                    sheet.Cell(row, 3).Value = outgoing;
                    sheet.Cell(row, 4).Value = internalCnt;
                    sheet.Cell(row, 5).Value = directiveCnt;
                    sheet.Cell(row, 6).Value = incoming + outgoing + internalCnt + directiveCnt;
                    totalIn += incoming; totalOut += outgoing;
                    totalInt += internalCnt; totalDir += directiveCnt;
                    row++;
                }

                sheet.Cell(row, 1).Value = "Итого";
                sheet.Cell(row, 2).Value = totalIn;
                sheet.Cell(row, 3).Value = totalOut;
                sheet.Cell(row, 4).Value = totalInt;
                sheet.Cell(row, 5).Value = totalDir;
                sheet.Cell(row, 6).Value = totalIn + totalOut + totalInt + totalDir;
                sheet.Range(row, 1, row, 6).Style.Font.Bold = true;
                sheet.Range(row, 1, row, 6).Style.Border.TopBorder = XLBorderStyleValues.Medium;

                sheet.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            }
        }

        public void ExportOverdueTasksReport(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу обязателен.", nameof(filePath));
            if (_tasks == null)
                throw new InvalidOperationException("ReportService не настроен для отчётов СЭД (нет ITaskService).");

            var now = DateTime.Now;
            var overdue = _tasks.ListOverdue(now);

            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.Worksheets.Add("Просроченные");
                sheet.Cell(1, 1).Value = "Просроченные поручения";
                sheet.Range(1, 1, 1, 7).Merge().Style.Font.SetBold(true).Font.SetFontSize(14)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                sheet.Cell(2, 1).Value = $"Сформировано: {now:dd.MM.yyyy HH:mm}; всего: {overdue.Count}";
                sheet.Range(2, 1, 2, 7).Merge();

                int hdr = 4;
                sheet.Cell(hdr, 1).Value = "№ поручения";
                sheet.Cell(hdr, 2).Value = "Документ";
                sheet.Cell(hdr, 3).Value = "Автор";
                sheet.Cell(hdr, 4).Value = "Исполнитель";
                sheet.Cell(hdr, 5).Value = "Срок";
                sheet.Cell(hdr, 6).Value = "Дней просрочки";
                sheet.Cell(hdr, 7).Value = "Описание";
                var headerRange = sheet.Range(hdr, 1, hdr, 7);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
                headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Medium;

                int row = hdr + 1;
                foreach (var t in overdue)
                {
                    sheet.Cell(row, 1).Value = t.Id;
                    sheet.Cell(row, 2).Value = t.Document?.RegistrationNumber ?? $"#{t.DocumentId}";
                    sheet.Cell(row, 3).Value = t.Author?.FullName ?? string.Empty;
                    sheet.Cell(row, 4).Value = t.Executor?.FullName ?? string.Empty;
                    sheet.Cell(row, 5).Value = t.Deadline.ToString("dd.MM.yyyy");
                    sheet.Cell(row, 6).Value = (int)Math.Ceiling((now - t.Deadline).TotalDays);
                    sheet.Cell(row, 7).Value = t.Description ?? string.Empty;
                    row++;
                }

                sheet.Columns().AdjustToContents();
                sheet.Column(7).Width = Math.Min(80, sheet.Column(7).Width);
                workbook.SaveAs(filePath);
            }
        }

        public void ExportNomenclatureAnalyticsReport(DateTime from, DateTime to, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Путь к файлу обязателен.", nameof(filePath));
            if (_nomenclature == null)
                throw new InvalidOperationException("ReportService не настроен для отчётов СЭД (нет INomenclatureRepository).");

            // Аналогично ExportDocumentVolumeReport: расширяем `to` до конца
            // дня, чтобы захватить документы, зарегистрированные после полуночи
            // на последний день периода.
            var toInclusive = ExtendToEndOfDay(to);
            var docs = _documents.Search(new DocumentSearchFilter { From = from, To = toInclusive });
            var cases = _nomenclature.ListCases(year: null, activeOnly: false);
            var depts = _nomenclature.ListDepartments();

            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.Worksheets.Add("Номенклатура");
                sheet.Cell(1, 1).Value = "Аналитика по номенклатуре дел";
                sheet.Range(1, 1, 1, 6).Merge().Style.Font.SetBold(true).Font.SetFontSize(14)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
                sheet.Cell(2, 1).Value = $"Период: {from:dd.MM.yyyy} — {to:dd.MM.yyyy}";
                sheet.Range(2, 1, 2, 6).Merge();

                int hdr = 4;
                sheet.Cell(hdr, 1).Value = "Индекс";
                sheet.Cell(hdr, 2).Value = "Дело";
                sheet.Cell(hdr, 3).Value = "Отдел";
                sheet.Cell(hdr, 4).Value = "Срок хранения, лет";
                sheet.Cell(hdr, 5).Value = "Документов за период";
                sheet.Cell(hdr, 6).Value = "Активно";
                var headerRange = sheet.Range(hdr, 1, hdr, 6);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
                headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Medium;

                int row = hdr + 1;
                foreach (var c in cases.OrderBy(x => x.Index))
                {
                    int count = docs.Count(d => d.NomenclatureCaseId == c.Id);
                    var dept = depts.FirstOrDefault(x => x.Id == c.DepartmentId);
                    sheet.Cell(row, 1).Value = c.Index;
                    sheet.Cell(row, 2).Value = c.Title;
                    sheet.Cell(row, 3).Value = dept?.Name ?? "—";
                    sheet.Cell(row, 4).Value = c.RetentionPeriodYears;
                    sheet.Cell(row, 5).Value = count;
                    sheet.Cell(row, 6).Value = c.IsActive ? "Да" : "Нет";
                    row++;
                }

                sheet.Columns().AdjustToContents();
                workbook.SaveAs(filePath);
            }
        }

        private static string FormatDirection(DocumentDirection d)
        {
            switch (d)
            {
                case DocumentDirection.Incoming: return "Входящий";
                case DocumentDirection.Outgoing: return "Исходящий";
                case DocumentDirection.Internal: return "Внутренний";
                case DocumentDirection.Directive: return "Распорядительный";
                default: return d.ToString();
            }
        }

        /// <summary>
        /// Возвращает дату-время «конец суток» для значения, пришедшего из
        /// DatePicker (где время = 00:00). Используется при формировании
        /// отчётов с периодом, чтобы полуинтервал работал ожидаемо «по дни
        /// включительно». Если на вход уже передан момент времени с ненулевым
        /// временем — возвращается без изменений.
        /// </summary>
        private static DateTime ExtendToEndOfDay(DateTime to)
        {
            if (to.TimeOfDay == TimeSpan.Zero)
                return to.Date.AddDays(1).AddTicks(-1);
            return to;
        }

        private static string FormatDocumentType(DocumentType t)
        {
            switch (t)
            {
                case DocumentType.Internal: return "Внутренний";
                case DocumentType.Archive: return "Архивный";
                case DocumentType.It: return "ИТ";
                default: return t.ToString();
            }
        }

        private static string FormatDocumentStatus(DocumentStatus s)
        {
            switch (s)
            {
                case DocumentStatus.New: return "Новый";
                case DocumentStatus.InProgress: return "В работе";
                case DocumentStatus.OnHold: return "Отложен";
                case DocumentStatus.Completed: return "Завершён";
                case DocumentStatus.Cancelled: return "Отменён";
                default: return s.ToString();
            }
        }

        private static string SafeSheetName(string name)
        {
            // Excel-ограничения: ≤31 символ, без []:*?/\.
            var trimmed = (name ?? "Лист").Trim();
            foreach (var ch in new[] { '[', ']', ':', '*', '?', '/', '\\' })
                trimmed = trimmed.Replace(ch, ' ');
            if (trimmed.Length > 31) trimmed = trimmed.Substring(0, 31);
            return string.IsNullOrEmpty(trimmed) ? "Лист" : trimmed;
        }

        private static string FormatArchiveRequestKind(ArchiveRequestKind kind)
        {
            switch (kind)
            {
                case ArchiveRequestKind.SocialLegal:
                    return "социально-правовой запрос";
                case ArchiveRequestKind.Thematic:
                    return "тематический запрос";
                case ArchiveRequestKind.MunicipalLegalActCopy:
                    return "копия муниципального правового акта";
                case ArchiveRequestKind.PaidThematic:
                    return "платный тематический запрос";
                default:
                    return kind.ToString();
            }
        }

        private static string FormatCategory(InventoryCategory category)
        {
            switch (category)
            {
                case InventoryCategory.Stationery:
                    return "Канцелярские товары и бланки";
                case InventoryCategory.IT_Equipment:
                    return "Оргтехника, расходные материалы и связь";
                case InventoryCategory.Cleaning_Supplies:
                    return "Хозяйственные и эксплуатационные материалы";
                default:
                    return category.ToString();
            }
        }

        private static W.Paragraph Paragraph(string text)
        {
            var p = new W.Paragraph();
            var run = p.AppendChild(new W.Run());
            run.AppendChild(new W.Text(text) { Space = SpaceProcessingModeValues.Preserve });
            return p;
        }

        private static W.Paragraph Heading(string text)
        {
            var p = new W.Paragraph();
            var props = p.AppendChild(new W.ParagraphProperties());
            props.AppendChild(new W.Justification { Val = W.JustificationValues.Center });
            var run = p.AppendChild(new W.Run());
            var runProps = run.AppendChild(new W.RunProperties());
            runProps.AppendChild(new W.Bold());
            runProps.AppendChild(new W.FontSize { Val = "32" });
            run.AppendChild(new W.Text(text) { Space = SpaceProcessingModeValues.Preserve });
            return p;
        }
    }
}
