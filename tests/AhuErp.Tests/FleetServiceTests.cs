using System;
using System.Collections.Generic;
using AhuErp.Core.Models;
using AhuErp.Core.Services;
using Xunit;

namespace AhuErp.Tests
{
    public class FleetServiceTests
    {
        private readonly FleetService _service = new FleetService();

        private static Vehicle MakeVehicle(int id = 1, VehicleStatus status = VehicleStatus.Available)
        {
            return new Vehicle
            {
                Id = id,
                Model = "UAZ Patriot",
                LicensePlate = $"А{id:000}АА 64",
                CurrentStatus = status
            };
        }

        [Fact]
        public void BookVehicle_books_available_vehicle_and_flips_status()
        {
            var vehicle = MakeVehicle();
            var start = new DateTime(2026, 5, 1, 8, 0, 0);
            var end = new DateTime(2026, 5, 1, 18, 0, 0);

            var trip = _service.BookVehicle(vehicle, start, end);

            Assert.Equal(start, trip.StartDate);
            Assert.Equal(end, trip.EndDate);
            Assert.Equal(VehicleStatus.OnMission, vehicle.CurrentStatus);
            Assert.Contains(trip, vehicle.Trips);
        }

        [Fact]
        public void BookVehicle_throws_when_maintenance()
        {
            var vehicle = MakeVehicle(status: VehicleStatus.Maintenance);

            Assert.Throws<VehicleBookingException>(() => _service.BookVehicle(
                vehicle, DateTime.Now, DateTime.Now.AddHours(2)));
        }

        [Fact]
        public void BookVehicle_throws_when_end_not_after_start()
        {
            var vehicle = MakeVehicle();
            var now = DateTime.Now;

            Assert.Throws<VehicleBookingException>(() => _service.BookVehicle(vehicle, now, now));
            Assert.Throws<VehicleBookingException>(() => _service.BookVehicle(vehicle, now, now.AddMinutes(-5)));
        }

        [Fact]
        public void BookVehicle_throws_when_interval_overlaps_existing_trip()
        {
            var vehicle = MakeVehicle();
            vehicle.CurrentStatus = VehicleStatus.OnMission;
            var existing = new VehicleTrip
            {
                Id = 100,
                VehicleId = vehicle.Id,
                Vehicle = vehicle,
                StartDate = new DateTime(2026, 5, 10, 9, 0, 0),
                EndDate = new DateTime(2026, 5, 10, 17, 0, 0)
            };
            var trips = new List<VehicleTrip> { existing };

            Assert.Throws<VehicleBookingException>(() => _service.BookVehicle(
                vehicle,
                new DateTime(2026, 5, 10, 12, 0, 0),
                new DateTime(2026, 5, 10, 20, 0, 0),
                trips));
        }

        [Fact]
        public void BookVehicle_allows_back_to_back_intervals_without_overlap()
        {
            var vehicle = MakeVehicle();
            var morning = new VehicleTrip
            {
                Id = 100,
                VehicleId = vehicle.Id,
                Vehicle = vehicle,
                StartDate = new DateTime(2026, 5, 10, 9, 0, 0),
                EndDate = new DateTime(2026, 5, 10, 12, 0, 0)
            };
            var trips = new List<VehicleTrip> { morning };

            // Новая поездка начинается ровно тогда, когда закончилась старая — пересечения нет.
            var afternoon = _service.BookVehicle(vehicle,
                new DateTime(2026, 5, 10, 12, 0, 0),
                new DateTime(2026, 5, 10, 16, 0, 0),
                trips);

            Assert.NotNull(afternoon);
        }

        [Fact]
        public void BookVehicle_ignores_trips_for_other_vehicles()
        {
            var mine = MakeVehicle(1);
            var foreign = new VehicleTrip
            {
                Id = 200,
                VehicleId = 999,
                StartDate = new DateTime(2026, 5, 10, 9, 0, 0),
                EndDate = new DateTime(2026, 5, 10, 17, 0, 0)
            };

            var trip = _service.BookVehicle(
                mine,
                new DateTime(2026, 5, 10, 12, 0, 0),
                new DateTime(2026, 5, 10, 14, 0, 0),
                new[] { foreign });

            Assert.NotNull(trip);
        }
    }
}
