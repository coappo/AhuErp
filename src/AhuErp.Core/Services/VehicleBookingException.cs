using System;

namespace AhuErp.Core.Services
{
    /// <summary>
    /// Исключение, выбрасываемое при невозможности забронировать транспортное средство
    /// (конфликт интервалов, недоступный статус и т. п.).
    /// </summary>
    [Serializable]
    public class VehicleBookingException : InvalidOperationException
    {
        public VehicleBookingException(string message)
            : base(message)
        {
        }

        public VehicleBookingException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
