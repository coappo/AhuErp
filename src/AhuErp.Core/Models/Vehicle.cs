using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// Транспортное средство автопарка учреждения.
    /// </summary>
    public class Vehicle
    {
        public int Id { get; set; }

        [Required]
        [StringLength(128)]
        public string Model { get; set; }

        [Required]
        [StringLength(32)]
        public string LicensePlate { get; set; }

        public VehicleStatus CurrentStatus { get; set; } = VehicleStatus.Available;

        public virtual ICollection<VehicleTrip> Trips { get; set; } = new HashSet<VehicleTrip>();
    }
}
