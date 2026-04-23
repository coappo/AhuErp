using System.Collections.Generic;
using AhuErp.Core.Models;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Абстракция хранилища автопарка. На Phase 4 используется in-memory реализация;
    /// при переходе на EF6 подменяется адаптером над <see cref="Data.AhuDbContext"/>
    /// без изменений в сервисе и UI.
    /// </summary>
    public interface IVehicleRepository
    {
        IReadOnlyList<Vehicle> ListVehicles();

        Vehicle GetVehicle(int vehicleId);

        /// <summary>Возвращает все поездки выбранного ТС в хронологическом порядке.</summary>
        IReadOnlyList<VehicleTrip> ListTrips(int vehicleId);

        void AddVehicle(Vehicle vehicle);

        void AddTrip(VehicleTrip trip);
    }
}
