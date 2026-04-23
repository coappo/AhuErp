using System;
using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Реализация <see cref="IFleetService"/> — не зависит от EF, принимает коллекцию
    /// существующих поездок, что делает бизнес-логику удобно тестируемой.
    /// </summary>
    public class FleetService : IFleetService
    {
        public VehicleTrip BookVehicle(Vehicle vehicle, DateTime startDate, DateTime endDate,
            IEnumerable<VehicleTrip> existingTrips = null, int? documentId = null)
        {
            if (vehicle == null) throw new ArgumentNullException(nameof(vehicle));
            if (endDate <= startDate)
            {
                throw new VehicleBookingException(
                    "Дата окончания поездки должна быть строго позже даты начала.");
            }

            if (vehicle.CurrentStatus == VehicleStatus.Maintenance)
            {
                throw new VehicleBookingException(
                    $"Транспортное средство '{vehicle.LicensePlate}' находится на техническом обслуживании.");
            }

            if (existingTrips != null)
            {
                foreach (var trip in existingTrips)
                {
                    if (trip.VehicleId != vehicle.Id) continue;
                    if (trip.OverlapsWith(startDate, endDate))
                    {
                        throw new VehicleBookingException(
                            $"Транспортное средство '{vehicle.LicensePlate}' уже забронировано " +
                            $"с {trip.StartDate:yyyy-MM-dd HH:mm} по {trip.EndDate:yyyy-MM-dd HH:mm}.");
                    }
                }
            }

            var newTrip = new VehicleTrip
            {
                VehicleId = vehicle.Id,
                Vehicle = vehicle,
                StartDate = startDate,
                EndDate = endDate,
                DocumentId = documentId
            };

            vehicle.Trips.Add(newTrip);
            vehicle.CurrentStatus = VehicleStatus.OnMission;
            return newTrip;
        }
    }
}
