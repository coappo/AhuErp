using System;
using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Сервис бронирования транспортных средств автопарка.
    /// </summary>
    public interface IFleetService
    {
        /// <summary>
        /// Создаёт запись о поездке, если интервал свободен и статус ТС допускает бронирование.
        /// </summary>
        /// <exception cref="VehicleBookingException">Интервал занят или ТС на обслуживании.</exception>
        VehicleTrip BookVehicle(Vehicle vehicle, DateTime startDate, DateTime endDate,
            IEnumerable<VehicleTrip> existingTrips = null, int? documentId = null);
    }
}
