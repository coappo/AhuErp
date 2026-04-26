using System;
using System.Linq;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Тесты Task 7: связи хозяйственных операций с РКК через
    /// <see cref="InventoryTransaction.BasisDocumentId"/> и
    /// <see cref="VehicleTrip.BasisDocumentId"/>.
    /// </summary>
    public class BasisDocumentLinkTests
    {
        [Fact]
        public void Inventory_writeoff_records_basis_document_id()
        {
            var repo = new InMemoryInventoryRepository();
            repo.AddItem(new InventoryItem { Name = "Бумага", TotalQuantity = 50 });
            var item = repo.ListItems().Single();
            var service = new InventoryService(repo);

            var tx = service.ProcessTransaction(item.Id, -3, documentId: 42, userId: 7);

            Assert.Equal(42, tx.DocumentId);
            Assert.Equal(42, tx.BasisDocumentId);
            Assert.Equal(47, item.TotalQuantity);
        }

        [Fact]
        public void Inventory_intake_without_document_has_null_basis()
        {
            var repo = new InMemoryInventoryRepository();
            repo.AddItem(new InventoryItem { Name = "Картридж", TotalQuantity = 0 });
            var item = repo.ListItems().Single();
            var service = new InventoryService(repo);

            var tx = service.ProcessTransaction(item.Id, +5, documentId: null, userId: 7);

            Assert.Null(tx.DocumentId);
            Assert.Null(tx.BasisDocumentId);
        }

        [Fact]
        public void Fleet_book_records_basis_document_id()
        {
            var repo = new InMemoryVehicleRepository();
            var vehicle = new Vehicle
            {
                LicensePlate = "А777АА", Model = "Газель",
                CurrentStatus = VehicleStatus.Available
            };
            repo.AddVehicle(vehicle);
            var service = new FleetService(repo);

            var trip = service.BookVehicle(vehicle.Id, documentId: 99,
                startDate: new DateTime(2099, 5, 1, 8, 0, 0),
                endDate: new DateTime(2099, 5, 1, 18, 0, 0),
                driverName: "Иванов И.И.");

            Assert.Equal(99, trip.DocumentId);
            Assert.Equal(99, trip.BasisDocumentId);
            Assert.Equal("Иванов И.И.", trip.DriverName);
        }

        [Fact]
        public void Fleet_book_without_document_has_null_basis()
        {
            var vehicle = new Vehicle
            {
                LicensePlate = "Б555БВ", Model = "Lada",
                CurrentStatus = VehicleStatus.Available
            };
            var service = new FleetService(repository: null);
            var trip = service.BookVehicle(vehicle,
                startDate: new DateTime(2099, 6, 1, 8, 0, 0),
                endDate: new DateTime(2099, 6, 1, 18, 0, 0));
            Assert.Null(trip.DocumentId);
            Assert.Null(trip.BasisDocumentId);
        }
    }
}
