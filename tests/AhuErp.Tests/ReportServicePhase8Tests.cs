using System;
using System.Collections.Generic;
using System.IO;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using ClosedXML.Excel;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Тесты Phase 8 на новые отчёты СЭД: журнал регистрации, исполнительская
    /// дисциплина, объём документооборота, просроченные поручения, аналитика
    /// по номенклатуре. Все XLSX пишутся во временный каталог и затем читаются
    /// обратно через ClosedXML, чтобы проверить структуру сетки.
    /// </summary>
    public class ReportServicePhase8Tests : IDisposable
    {
        private readonly InMemoryInventoryRepository _inventory = new InMemoryInventoryRepository();
        private readonly InMemoryDocumentRepository _documents = new InMemoryDocumentRepository();
        private readonly InMemoryTaskRepository _taskRepo = new InMemoryTaskRepository();
        private readonly InMemoryAuditLogRepository _auditRepo = new InMemoryAuditLogRepository();
        private readonly InMemoryNomenclatureRepository _nomenclature = new InMemoryNomenclatureRepository();
        private readonly AuditService _audit;
        private readonly TaskService _tasks;
        private readonly ReportService _service;
        private readonly string _workdir;

        public ReportServicePhase8Tests()
        {
            _audit = new AuditService(_auditRepo);
            _tasks = new TaskService(_taskRepo, _documents, _audit);
            _service = new ReportService(_inventory, _documents, _tasks, _taskRepo, _nomenclature);
            _workdir = Path.Combine(Path.GetTempPath(), "AhuErpTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_workdir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_workdir, recursive: true); } catch { /* best-effort */ }
        }

        [Fact]
        public void ExportRegistrationJournal_writes_header_and_rows()
        {
            var d1 = new Document
            {
                Title = "Письмо",
                RegistrationNumber = "ВХ-1/2025",
                RegistrationDate = new DateTime(2025, 1, 10),
                Direction = DocumentDirection.Incoming,
                Type = DocumentType.Incoming,
                Correspondent = "ИФНС",
                Deadline = new DateTime(2025, 2, 1),
                Status = DocumentStatus.New
            };
            var d2 = new Document
            {
                Title = "Ответ",
                RegistrationNumber = "ИСХ-2/2025",
                RegistrationDate = new DateTime(2025, 2, 1),
                Direction = DocumentDirection.Outgoing,
                Type = DocumentType.Office,
                Correspondent = "ИФНС",
                Deadline = new DateTime(2025, 3, 1),
                Status = DocumentStatus.InProgress
            };

            var path = Path.Combine(_workdir, "journal.xlsx");
            _service.ExportRegistrationJournal(new[] { d1, d2 }, "Журнал входящих", path);

            Assert.True(File.Exists(path));
            using (var wb = new XLWorkbook(path))
            {
                var sheet = wb.Worksheet(1);
                Assert.Equal("Рег. №", sheet.Cell(4, 1).GetString());
                Assert.Equal("ВХ-1/2025", sheet.Cell(5, 1).GetString());
                Assert.Equal("ИСХ-2/2025", sheet.Cell(6, 1).GetString());
            }
        }

        [Fact]
        public void ExportExecutionDisciplineReport_summarizes_metrics()
        {
            var doc = new Document
            {
                Title = "Распоряжение",
                Type = DocumentType.Internal,
                CreationDate = new DateTime(2025, 1, 1),
                Deadline = new DateTime(2099, 1, 1),
                Status = DocumentStatus.New
            };
            _documents.Add(doc);

            // Используем repo напрямую: CreateTask требует Deadline в будущем,
            // а нам нужны исторические данные за фиксированный отчётный период.
            _taskRepo.AddTask(new DocumentTask
            {
                DocumentId = doc.Id,
                AuthorId = 1,
                ExecutorId = 5,
                Description = "В срок",
                CreatedAt = new DateTime(2025, 1, 1),
                Deadline = new DateTime(2025, 1, 31),
                Status = DocumentTaskStatus.Completed,
                CompletedAt = new DateTime(2025, 1, 20)
            });
            _taskRepo.AddTask(new DocumentTask
            {
                DocumentId = doc.Id,
                AuthorId = 1,
                ExecutorId = 5,
                Description = "Опоздал",
                CreatedAt = new DateTime(2025, 1, 1),
                Deadline = new DateTime(2025, 1, 5),
                Status = DocumentTaskStatus.Completed,
                CompletedAt = new DateTime(2025, 1, 10)
            });

            var path = Path.Combine(_workdir, "discipline.xlsx");
            _service.ExportExecutionDisciplineReport(
                new DateTime(2025, 1, 1), new DateTime(2025, 12, 31), path);

            Assert.True(File.Exists(path));
            using (var wb = new XLWorkbook(path))
            {
                var sheet = wb.Worksheet(1);
                // Ячейки общей статистики
                Assert.Equal("Всего поручений", sheet.Cell(4, 1).GetString());
                Assert.Equal(2, (int)sheet.Cell(4, 2).GetDouble());
                Assert.Equal("Исполнитель", sheet.Cell(10, 1).GetString());
            }
        }

        [Fact]
        public void ExportDocumentVolumeReport_groups_by_direction()
        {
            _documents.Add(new Document
            {
                Title = "in", Direction = DocumentDirection.Incoming, Type = DocumentType.Incoming,
                CreationDate = new DateTime(2025, 1, 5),
                Deadline = new DateTime(2025, 2, 1)
            });
            _documents.Add(new Document
            {
                Title = "out", Direction = DocumentDirection.Outgoing, Type = DocumentType.Office,
                CreationDate = new DateTime(2025, 1, 6),
                Deadline = new DateTime(2025, 2, 1)
            });
            _documents.Add(new Document
            {
                Title = "int", Direction = DocumentDirection.Internal, Type = DocumentType.Internal,
                CreationDate = new DateTime(2025, 1, 7),
                Deadline = new DateTime(2025, 2, 1)
            });

            var path = Path.Combine(_workdir, "volume.xlsx");
            _service.ExportDocumentVolumeReport(
                new DateTime(2025, 1, 1), new DateTime(2025, 12, 31), path);

            using (var wb = new XLWorkbook(path))
            {
                var sheet = wb.Worksheet(1);
                Assert.Equal("Вид документа", sheet.Cell(4, 1).GetString());
                Assert.Equal("Входящих", sheet.Cell(4, 2).GetString());
                Assert.Equal("Исходящих", sheet.Cell(4, 3).GetString());
                Assert.Equal("Внутренних", sheet.Cell(4, 4).GetString());
            }
        }

        [Fact]
        public void ExportOverdueTasksReport_lists_overdue_only()
        {
            var doc = new Document
            {
                Title = "Док",
                Type = DocumentType.Internal,
                Deadline = new DateTime(2099, 1, 1)
            };
            _documents.Add(doc);

            // Просроченное
            _taskRepo.AddTask(new DocumentTask
            {
                DocumentId = doc.Id,
                AuthorId = 1,
                ExecutorId = 5,
                Description = "Просрочено",
                CreatedAt = DateTime.Now.AddDays(-30),
                Deadline = DateTime.Now.AddDays(-1),
                Status = DocumentTaskStatus.InProgress
            });
            // Не просроченное (закрыто)
            _taskRepo.AddTask(new DocumentTask
            {
                DocumentId = doc.Id,
                AuthorId = 1,
                ExecutorId = 5,
                Description = "Готово",
                CreatedAt = DateTime.Now.AddDays(-30),
                Deadline = DateTime.Now.AddDays(-1),
                Status = DocumentTaskStatus.Completed,
                CompletedAt = DateTime.Now.AddDays(-2)
            });

            var path = Path.Combine(_workdir, "overdue.xlsx");
            _service.ExportOverdueTasksReport(path);

            using (var wb = new XLWorkbook(path))
            {
                var sheet = wb.Worksheet(1);
                Assert.Equal("№ поручения", sheet.Cell(4, 1).GetString());
                // Должна быть ровно одна строка просрочки
                Assert.Equal("Просрочено", sheet.Cell(5, 7).GetString());
                // 6-й строки данных не должно быть
                Assert.True(string.IsNullOrEmpty(sheet.Cell(6, 1).GetString()));
            }
        }

        [Fact]
        public void ExportNomenclatureAnalyticsReport_counts_documents_per_case()
        {
            var dept = _nomenclature.AddDepartment(new Department { Name = "АХУ" });
            var c1 = _nomenclature.AddCase(new NomenclatureCase
            {
                Index = "01-05",
                Title = "Распоряжения АХУ",
                DepartmentId = dept.Id,
                RetentionPeriodYears = 5,
                Year = 2025,
                IsActive = true
            });
            var c2 = _nomenclature.AddCase(new NomenclatureCase
            {
                Index = "01-06",
                Title = "Хозяйственные документы",
                DepartmentId = dept.Id,
                RetentionPeriodYears = 3,
                Year = 2025,
                IsActive = true
            });

            _documents.Add(new Document
            {
                Title = "Док-1", Type = DocumentType.Internal,
                CreationDate = new DateTime(2025, 1, 5),
                Deadline = new DateTime(2025, 2, 1),
                NomenclatureCaseId = c1.Id
            });
            _documents.Add(new Document
            {
                Title = "Док-2", Type = DocumentType.Internal,
                CreationDate = new DateTime(2025, 1, 6),
                Deadline = new DateTime(2025, 2, 1),
                NomenclatureCaseId = c1.Id
            });

            var path = Path.Combine(_workdir, "nomen.xlsx");
            _service.ExportNomenclatureAnalyticsReport(
                new DateTime(2025, 1, 1), new DateTime(2025, 12, 31), path);

            using (var wb = new XLWorkbook(path))
            {
                var sheet = wb.Worksheet(1);
                Assert.Equal("Индекс", sheet.Cell(4, 1).GetString());
                // По делу c1 — 2 документа, по делу c2 — 0
                bool foundC1Two = false;
                bool foundC2Zero = false;
                for (int row = 5; row <= 10; row++)
                {
                    var idx = sheet.Cell(row, 1).GetString();
                    if (idx == c1.Index && (int)sheet.Cell(row, 5).GetDouble() == 2) foundC1Two = true;
                    if (idx == c2.Index && (int)sheet.Cell(row, 5).GetDouble() == 0) foundC2Zero = true;
                }
                Assert.True(foundC1Two, "Дело c1 должно содержать 2 документа.");
                Assert.True(foundC2Zero, "Дело c2 должно содержать 0 документов.");
            }
        }

        [Fact]
        public void NewReportMethods_throw_when_legacy_constructor_used()
        {
            var legacy = new ReportService(_inventory, _documents);
            var path = Path.Combine(_workdir, "x.xlsx");

            Assert.Throws<InvalidOperationException>(() =>
                legacy.ExportExecutionDisciplineReport(
                    new DateTime(2025, 1, 1), new DateTime(2025, 12, 31), path));
            Assert.Throws<InvalidOperationException>(() =>
                legacy.ExportOverdueTasksReport(path));
            Assert.Throws<InvalidOperationException>(() =>
                legacy.ExportNomenclatureAnalyticsReport(
                    new DateTime(2025, 1, 1), new DateTime(2025, 12, 31), path));
        }
    }
}
