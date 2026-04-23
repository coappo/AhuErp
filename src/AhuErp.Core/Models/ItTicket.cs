using System.ComponentModel.DataAnnotations;

namespace AhuErp.Core.Models
{
    /// <summary>
    /// IT-заявка Help Desk. Модель наследуется от <see cref="Document"/> через
    /// TPH-дискриминатор — это даёт единый Id-контур с остальными документами
    /// (включая связь с <see cref="InventoryTransaction.DocumentId"/>).
    /// </summary>
    public class ItTicket : Document
    {
        [StringLength(256)]
        public string AffectedEquipment { get; set; }

        [StringLength(1024)]
        public string ResolutionNotes { get; set; }

        public ItTicket()
        {
            Type = DocumentType.It;
        }
    }
}
