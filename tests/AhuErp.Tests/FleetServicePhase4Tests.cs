using System;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using Xunit;

namespace AhuErp.Tests
{
    /// <summary>
    /// Тесты Phase 4-перегрузки <see cref="FleetService.BookVehicle(int, int, DateTime, DateTime, string)"/>,
    /// работающей через <see cref="IVehicleRepository"/>.
    /// </summary>
    public class FleetServicePhase4Tests
    {
        private readonly InMemoryVehicleRepository _repo;
        private readonly FleetService _service;

        public FleetServicePhase4Tests()
        {
            _repo = new InMemoryVehicleRepository();
            _service = new FleetService(_repo);

            _repo.AddVehicle(new Vehicle
            {
                Model = "Ford Focus",
                LicensePlate = "А001АА",
                CurrentStatus = VehicleStatus.Available
            });
        }

        [Fact]
        public void BookVehicle_succeeds_when_no_overlap()
        {
            var first = _service.BookVehicle(
                vehicleId: 1,
                documentId: 10,
                startDate: new DateTime(2026, 5, 1, 9, 0, 0),
                endDate: new DateTime(2026, 5, 1, 12, 0, 0),
                driverName: "Иванов И.И.");

            var second = _service.BookVehicle(
                vehicleId: 1,
                documentId: 11,
                startDate: new DateTime(2026, 5, 1, 13, 0, 0),
                endDate: new DateTime(2026, 5, 1, 17, 0, 0),
                driverName: "Петров П.П.");

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.Equal(2, _repo.ListTrips(1).Count);
            Assert.Equal("Иванов И.И.", first.DriverName);
            Assert.Equal(10, first.DocumentId);
        }

        [Fact]
        public void BookVehicle_throws_on_exact_overlap()
        {
            _service.BookVehicle(1, 10,
                new DateTime(2026, 5, 1, 9, 0, 0),
                new DateTime(2026, 5, 1, 12, 0, 0),
                "Иванов И.И.");

            var ex = Assert.Throws<VehicleBookingException>(() =>
                _service.BookVehicle(1, 11,
                    new DateTime(2026, 5, 1, 9, 0, 0),
                    new DateTime(2026, 5, 1, 12, 0, 0),
                    "Петров П.П."));

            Assert.Contains("уже забронирован", ex.Message);
            Assert.Single(_repo.ListTrips(1));
        }

        [Fact]
        public void BookVehicle_throws_on_partial_overlap_left()
        {
            _service.BookVehicle(1, 10,
                new DateTime(2026, 5, 1, 10, 0, 0),
                new DateTime(2026, 5, 1, 14, 0, 0),
                "Иванов И.И.");

            Assert.Throws<VehicleBookingException>(() =>
                _service.BookVehicle(1, 11,
                    new DateTime(2026, 5, 1, 9, 0, 0),
                    new DateTime(2026, 5, 1, 11, 0, 0),
                    "Петров П.П."));
        }

        [Fact]
        public void BookVehicle_throws_on_partial_overlap_right()
        {
            _service.BookVehicle(1, 10,
                new DateTime(2026, 5, 1, 10, 0, 0),
                new DateTime(2026, 5, 1, 14, 0, 0),
                "Иванов И.И.");

            Assert.Throws<VehicleBookingException>(() =>
                _service.BookVehicle(1, 11,
                    new DateTime(2026, 5, 1, 13, 0, 0),
                    new DateTime(2026, 5, 1, 16, 0, 0),
                    "Петров П.П."));
        }

        [Fact]
        public void BookVehicle_throws_on_contained_overlap()
        {
            _service.BookVehicle(1, 10,
                new DateTime(2026, 5, 1, 9, 0, 0),
                new DateTime(2026, 5, 1, 18, 0, 0),
                "Иванов И.И.");

            Assert.Throws<VehicleBookingException>(() =>
                _service.BookVehicle(1, 11,
                    new DateTime(2026, 5, 1, 12, 0, 0),
                    new DateTime(2026, 5, 1, 14, 0, 0),
                    "Петров П.П."));
        }

        [Fact]
        public void BookVehicle_allows_back_to_back_intervals()
        {
            _service.BookVehicle(1, 10,
                new DateTime(2026, 5, 1, 9, 0, 0),
                new DateTime(2026, 5, 1, 12, 0, 0),
                "Иванов И.И.");

            // Конец предыдущей == начало следующей → intervals [a,b) не пересекаются.
            var next = _service.BookVehicle(1, 11,
                new DateTime(2026, 5, 1, 12, 0, 0),
                new DateTime(2026, 5, 1, 15, 0, 0),
                "Петров П.П.");

            Assert.NotNull(next);
            Assert.Equal(2, _repo.ListTrips(1).Count);
        }

        [Fact]
        public void BookVehicle_throws_for_missing_vehicle()
        {
            Assert.Throws<VehicleBookingException>(() =>
                _service.BookVehicle(99, 10,
                    new DateTime(2026, 5, 1, 9, 0, 0),
                    new DateTime(2026, 5, 1, 12, 0, 0),
                    "Иванов И.И."));
        }

        [Fact]
        public void BookVehicle_throws_when_vehicle_in_maintenance()
        {
            var v = _repo.GetVehicle(1);
            v.CurrentStatus = VehicleStatus.Maintenance;

            var ex = Assert.Throws<VehicleBookingException>(() =>
                _service.BookVehicle(1, 10,
                    new DateTime(2026, 5, 1, 9, 0, 0),
                    new DateTime(2026, 5, 1, 12, 0, 0),
                    "Иванов И.И."));

            Assert.Contains("обслуживании", ex.Message);
            Assert.Empty(_repo.ListTrips(1));
        }

        [Fact]
        public void BookVehicle_requires_driver_name()
        {
            Assert.Throws<ArgumentException>(() =>
                _service.BookVehicle(1, 10,
                    new DateTime(2026, 5, 1, 9, 0, 0),
                    new DateTime(2026, 5, 1, 12, 0, 0),
                    "  "));
        }

        [Fact]
        public void BookVehicle_requires_document_id()
        {
            Assert.Throws<ArgumentException>(() =>
                _service.BookVehicle(1, 0,
                    new DateTime(2026, 5, 1, 9, 0, 0),
                    new DateTime(2026, 5, 1, 12, 0, 0),
                    "Иванов И.И."));
        }
    }
}
