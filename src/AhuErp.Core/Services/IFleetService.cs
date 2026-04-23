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
        /// Функциональная перегрузка (без состояния): создаёт запись о поездке,
        /// если интервал свободен и статус ТС допускает бронирование. Удобна
        /// в юнит-тестах и сохраняется для обратной совместимости.
        /// </summary>
        /// <exception cref="VehicleBookingException">Интервал занят или ТС на обслуживании.</exception>
        VehicleTrip BookVehicle(Vehicle vehicle, DateTime startDate, DateTime endDate,
            IEnumerable<VehicleTrip> existingTrips = null, int? documentId = null);

        /// <summary>
        /// Основной API Phase 4: бронирует ТС через <see cref="IVehicleRepository"/>.
        /// Проверяет пересечение интервалов по Allen-алгоритму
        /// (<c>existing.StartDate &lt; endDate &amp;&amp; existing.EndDate &gt; startDate</c>)
        /// и при успехе сохраняет <see cref="VehicleTrip"/> в репозитории.
        /// </summary>
        /// <param name="vehicleId">Идентификатор ТС.</param>
        /// <param name="documentId">Обязательный документ-основание (заявка на транспорт).</param>
        /// <param name="startDate">Дата/время начала поездки.</param>
        /// <param name="endDate">Дата/время окончания (строго позже начала).</param>
        /// <param name="driverName">ФИО водителя (обязательно).</param>
        /// <exception cref="VehicleBookingException">Интервал занят, ТС на обслуживании или не найдено.</exception>
        /// <exception cref="ArgumentException">Некорректные параметры.</exception>
        VehicleTrip BookVehicle(int vehicleId, int documentId, DateTime startDate, DateTime endDate, string driverName);
    }
}
