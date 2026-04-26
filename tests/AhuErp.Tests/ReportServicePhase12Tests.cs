using System;
using System.IO;
using System.Linq;
using System.Text;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Phase 12 — пакет регламентированных отчётов СЭД (XLSX, DOCX, PDF).
    /// XLSX/DOCX проверяем структурно через ClosedXML и WordprocessingDocument.
    /// PDF — по сигнатуре «%PDF-» и минимальному размеру.
    /// </summary>
    public class ReportServicePhase12Tests : IDisposable
    {
        private readonly InMemoryInventoryRepository _inventory = new InMemoryInventoryRepository();
        private readonly InMemoryDocumentRepository _documents = new InMemoryDocumentRepository();
        private readonly InMemoryTaskRepository _taskRepo = new InMemoryTaskRepository();
        private readonly InMemoryAuditLogRepository _auditRepo = new InMemoryAuditLogRepository();
        private readonly InMemoryNomenclatureRepository _nomenclature = new InMemoryNomenclatureRepository();
        private readonly InMemoryVehicleRepository _vehicles = new InMemoryVehicleRepository();
        private readonly AuditService _audit;
        private readonly TaskService _tasks;
        private readonly ReportService _service;
        private readonly string _workdir;

        public ReportServicePhase12Tests()
        {
            _audit = new AuditService(_auditRepo);
            _tasks = new TaskService(_taskRepo, _documents, _audit);
            _service = new ReportService(_inventory, _documents, _tasks, _taskRepo,
                _nomenclature, _vehicles, _audit);
            _workdir = Path.Combine(Path.GetTempPath(), "AhuErpTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_workdir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_workdir, recursive: true); } catch { /* best-effort */ }
        }

        // ----------------------------- Outgoing dispatch -----------------

        [Fact]
        public void OutgoingDispatchRegistry_includes_only_outgoing_in_period()
        {
            _documents.Add(new Document
            {
                Title = "Ответ",
                Type = DocumentType.Office,
                CreationDate = DateTime.Now,
                Deadline = DateTime.Now.AddDays(7),
                Direction = DocumentDirection.Outgoing,
                Correspondent = "ИФНС",
                RegistrationNumber = "ИСХ-1/2026",
                RegistrationDate = new DateTime(2026, 1, 15),
            });
            _documents.Add(new Document
            {
                Title = "Письмо",
                Type = DocumentType.Incoming,
                CreationDate = DateTime.Now,
                Deadline = DateTime.Now.AddDays(7),
                Direction = DocumentDirection.Incoming,
                RegistrationNumber = "ВХ-1/2026",
                RegistrationDate = new DateTime(2026, 1, 16),
            });

            var path = Path.Combine(_workdir, "dispatch.xlsx");
            _service.ExportOutgoingDispatchRegistry(
                new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), path);

            Assert.True(new FileInfo(path).Length > 0);
            using (var wb = new XLWorkbook(path))
            {
                var sheet = wb.Worksheet(1);
                Assert.Equal("ИСХ-1/2026", sheet.Cell(3, 2).GetString());
                Assert.Contains("Всего отправлено: 1", sheet.Cell(4, 1).GetString());
            }
        }

        // ----------------------------- Case inventory --------------------

        [Fact]
        public void GenerateCaseInventory_writes_docx_with_all_documents()
        {
            var @case = _nomenclature.AddCase(new NomenclatureCase
            {
                Index = "01-08",
                Title = "Переписка по налогам",
                Year = 2026,
                RetentionPeriodYears = 5,
                IsActive = true,
            });
            for (int i = 1; i <= 3; i++)
            {
                _documents.Add(new Document
                {
                    Title = $"Документ {i}",
                    Type = DocumentType.Office,
                    CreationDate = DateTime.Now,
                    Deadline = DateTime.Now.AddDays(7),
                    NomenclatureCaseId = @case.Id,
                    RegistrationNumber = $"OFF-{i}/2026",
                    RegistrationDate = new DateTime(2026, i, 1),
                });
            }

            var path = Path.Combine(_workdir, "inventory.docx");
            _service.GenerateCaseInventory(@case.Id, path);

            Assert.True(File.Exists(path));
            using (var d = WordprocessingDocument.Open(path, isEditable: false))
            {
                var text = d.MainDocumentPart.Document.Body.InnerText;
                Assert.Contains("ОПИСЬ ДЕЛА № 01-08", text);
                Assert.Contains("OFF-1/2026", text);
                Assert.Contains("OFF-2/2026", text);
                Assert.Contains("OFF-3/2026", text);
                Assert.Contains("Всего в дело включено документов: 3", text);
            }
        }

        [Fact]
        public void GenerateCaseInventory_throws_for_unknown_case()
        {
            var path = Path.Combine(_workdir, "x.docx");
            Assert.Throws<InvalidOperationException>(() =>
                _service.GenerateCaseInventory(nomenclatureCaseId: 9999, path));
        }

        // ----------------------------- Fleet -----------------------------

        [Fact]
        public void FleetReport_aggregates_trips_and_idle_hours()
        {
            var v = new Vehicle { Model = "ГАЗ-3221", LicensePlate = "А001АА777" };
            _vehicles.AddVehicle(v);
            _vehicles.AddTrip(new VehicleTrip
            {
                VehicleId = v.Id,
                StartDate = new DateTime(2026, 1, 5, 9, 0, 0),
                EndDate = new DateTime(2026, 1, 5, 12, 0, 0),
                DriverName = "Иванов",
            });

            var path = Path.Combine(_workdir, "fleet.xlsx");
            _service.ExportFleetReport(
                new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), path);

            using (var wb = new XLWorkbook(path))
            {
                var sheet = wb.Worksheet(1);
                Assert.Equal("ГАЗ-3221", sheet.Cell(3, 1).GetString());
                Assert.Equal(1, sheet.Cell(3, 3).GetValue<int>());
                Assert.Equal(3.0, sheet.Cell(3, 4).GetValue<double>());
            }
        }

        [Fact]
        public void FleetReport_validates_period_order()
        {
            var path = Path.Combine(_workdir, "fleet-bad.xlsx");
            Assert.Throws<ArgumentException>(() =>
                _service.ExportFleetReport(
                    new DateTime(2026, 5, 1), new DateTime(2026, 1, 1), path));
        }

        // ----------------------------- Inventory turnover ---------------

        [Fact]
        public void InventoryTurnoverReport_balances_opening_in_out_closing()
        {
            var item = new InventoryItem { Name = "Бумага A4", Category = InventoryCategory.Stationery };
            _inventory.AddItem(item);
            // Остаток на 01.01.2026 = 10 (приход в декабре).
            _inventory.RecordTransaction(new InventoryTransaction
            {
                InventoryItemId = item.Id,
                QuantityChanged = 10,
                TransactionDate = new DateTime(2025, 12, 15),
                InitiatorId = 1,
            });
            // В январе: приход +20, расход -5.
            _inventory.RecordTransaction(new InventoryTransaction
            {
                InventoryItemId = item.Id,
                QuantityChanged = 20,
                TransactionDate = new DateTime(2026, 1, 10),
                InitiatorId = 1,
            });
            _inventory.RecordTransaction(new InventoryTransaction
            {
                InventoryItemId = item.Id,
                QuantityChanged = -5,
                TransactionDate = new DateTime(2026, 1, 20),
                InitiatorId = 1,
            });

            var path = Path.Combine(_workdir, "turnover.xlsx");
            _service.ExportInventoryTurnoverReport(
                new DateTime(2026, 1, 1), new DateTime(2026, 1, 31), path);

            using (var wb = new XLWorkbook(path))
            {
                var sheet = wb.Worksheet(1);
                Assert.Equal("Бумага A4", sheet.Cell(3, 1).GetString());
                Assert.Equal(10, sheet.Cell(3, 3).GetValue<int>()); // opening
                Assert.Equal(20, sheet.Cell(3, 4).GetValue<int>()); // in
                Assert.Equal(5, sheet.Cell(3, 5).GetValue<int>());  // out
                Assert.Equal(25, sheet.Cell(3, 6).GetValue<int>()); // closing
            }
        }

        // ----------------------------- Audit trail PDF -------------------

        [Fact]
        public void AuditTrail_pdf_starts_with_signature_and_is_non_trivial()
        {
            var doc = new Document
            {
                Title = "Документ",
                Type = DocumentType.Office,
                CreationDate = DateTime.Now,
                Deadline = DateTime.Now.AddDays(7),
                RegistrationNumber = "OFF-AT-1",
            };
            _documents.Add(doc);

            _audit.Record(AuditActionType.Created, nameof(Document), doc.Id, userId: 1,
                newValues: "Заголовок=Документ");
            _audit.Record(AuditActionType.StatusChanged, nameof(Document), doc.Id, userId: 1,
                newValues: "Status=InProgress", details: "Перевели в работу");
            _audit.Record(AuditActionType.Registered, nameof(Document), doc.Id, userId: 2,
                newValues: "RegistrationNumber=OFF-AT-1");

            var path = Path.Combine(_workdir, "audit.pdf");
            _service.ExportDocumentAuditTrail(doc.Id, path);

            var bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length > 1024, $"PDF too small: {bytes.Length} bytes");
            var head = Encoding.ASCII.GetString(bytes, 0, 5);
            Assert.Equal("%PDF-", head);
        }

        [Fact]
        public void AuditTrail_throws_when_audit_or_document_missing()
        {
            var path = Path.Combine(_workdir, "xx.pdf");
            // Документ #999 не существует → ошибка.
            Assert.Throws<InvalidOperationException>(() =>
                _service.ExportDocumentAuditTrail(999, path));

            // Сервис без IAuditService: ожидаем явную ошибку.
            var noAudit = new ReportService(_inventory, _documents, _tasks, _taskRepo,
                _nomenclature, _vehicles, audit: null);
            var doc = new Document
            {
                Title = "X",
                Type = DocumentType.Office,
                CreationDate = DateTime.Now,
                Deadline = DateTime.Now.AddDays(1),
                RegistrationNumber = "X-1"
            };
            _documents.Add(doc);
            Assert.Throws<InvalidOperationException>(() =>
                noAudit.ExportDocumentAuditTrail(doc.Id, path));
        }

        [Fact]
        public void OutgoingDispatchRegistry_validates_period_order()
        {
            var path = Path.Combine(_workdir, "x.xlsx");
            Assert.Throws<ArgumentException>(() =>
                _service.ExportOutgoingDispatchRegistry(
                    new DateTime(2026, 5, 1), new DateTime(2026, 1, 1), path));
        }
    }
}
