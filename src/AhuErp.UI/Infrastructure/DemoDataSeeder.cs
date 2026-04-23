using System;
using AhuErp.Core.Models;
using AhuErp.Core.Services;

namespace AhuErp.UI.Infrastructure
{
    /// <summary>
    /// Заполняет in-memory репозитории демонстрационными данными для Phase 2.
    /// В production будет заменено на реальный <c>AhuDbContext</c>.
    /// </summary>
    public static class DemoDataSeeder
    {
        public const string DefaultPassword = "password";

        public static void Seed(InMemoryEmployeeRepository employees,
                                InMemoryDocumentRepository documents,
                                IPasswordHasher hasher)
        {
            if (employees == null) throw new ArgumentNullException(nameof(employees));
            if (documents == null) throw new ArgumentNullException(nameof(documents));
            if (hasher == null) throw new ArgumentNullException(nameof(hasher));

            var hash = hasher.Hash(DefaultPassword);

            var admin = new Employee
            {
                Id = 1,
                FullName = "Иванов Иван Иванович",
                Position = "Администратор",
                Role = EmployeeRole.Admin,
                PasswordHash = hash
            };
            var manager = new Employee
            {
                Id = 2,
                FullName = "Петров Пётр Петрович",
                Position = "Руководитель АХУ",
                Role = EmployeeRole.Manager,
                PasswordHash = hash
            };
            var archivist = new Employee
            {
                Id = 3,
                FullName = "Сидорова Анна Сергеевна",
                Position = "Архивариус",
                Role = EmployeeRole.Archivist,
                PasswordHash = hash
            };
            var tech = new Employee
            {
                Id = 4,
                FullName = "Кузнецов Алексей Викторович",
                Position = "Инженер IT",
                Role = EmployeeRole.TechSupport,
                PasswordHash = hash
            };
            var warehouse = new Employee
            {
                Id = 5,
                FullName = "Орлова Мария Николаевна",
                Position = "Заведующий хозяйством",
                Role = EmployeeRole.WarehouseManager,
                PasswordHash = hash
            };

            employees.Add(admin);
            employees.Add(manager);
            employees.Add(archivist);
            employees.Add(tech);
            employees.Add(warehouse);

            var now = DateTime.Now;

            documents.Add(new Document
            {
                Type = DocumentType.Incoming,
                Title = "Письмо Министерства от 15.04",
                CreationDate = now.AddDays(-10),
                Deadline = now.AddDays(5),
                Status = DocumentStatus.InProgress,
                AssignedEmployeeId = manager.Id
            });
            documents.Add(new Document
            {
                Type = DocumentType.Internal,
                Title = "Распоряжение о субботнике",
                CreationDate = now.AddDays(-3),
                Deadline = now.AddDays(2),
                Status = DocumentStatus.New,
                AssignedEmployeeId = warehouse.Id
            });

            var archive = new ArchiveRequest
            {
                Title = "Архивная справка — Сидорова",
                Status = DocumentStatus.InProgress,
                HasPassportScan = true,
                HasWorkBookScan = false,
                AssignedEmployeeId = archivist.Id
            };
            archive.InitializeDeadline(now.AddDays(-25));
            documents.Add(archive);

            documents.Add(new ItTicket
            {
                Title = "Не печатает принтер HP LaserJet в каб. 214",
                AffectedEquipment = "HP LaserJet Pro M404",
                CreationDate = now.AddDays(-2),
                Deadline = now.AddDays(3),
                Status = DocumentStatus.InProgress,
                AssignedEmployeeId = tech.Id
            });
        }

        public static void SeedInventory(InMemoryInventoryRepository inventory)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));

            inventory.AddItem(new InventoryItem
            {
                Name = "Бумага А4 500 л.",
                Category = InventoryCategory.Stationery,
                TotalQuantity = 40
            });
            inventory.AddItem(new InventoryItem
            {
                Name = "Ручка шариковая синяя",
                Category = InventoryCategory.Stationery,
                TotalQuantity = 120
            });
            inventory.AddItem(new InventoryItem
            {
                Name = "Картридж HP 59A",
                Category = InventoryCategory.IT_Equipment,
                TotalQuantity = 6
            });
            inventory.AddItem(new InventoryItem
            {
                Name = "Жидкость для мытья полов 5 л",
                Category = InventoryCategory.Cleaning_Supplies,
                TotalQuantity = 8
            });
        }
    }
}
