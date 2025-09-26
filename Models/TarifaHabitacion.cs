using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PIOGHOASIS.Models
{
    [Table("TARIFA_HABITACION")]
    public class TarifaHabitacion
    {
        [Key] public int TarifaID { get; set; }

        [Required] public int HabitacionID { get; set; }

        [Required, Range(1, 99)]
        public int NumeroPersonas { get; set; }

        [Required, Column(TypeName = "decimal(10,2)")]
        [Range(0.01, 99999999.99)]
        public decimal PrecioNoche { get; set; }

        [Required, DataType(DataType.Date)]
        public DateTime FechaInicio { get; set; }

        [Required, DataType(DataType.Date)]
        public DateTime FechaFin { get; set; }

        [StringLength(50)]
        public string? EtiquetaTemporada { get; set; }

        // NAV
        [ForeignKey(nameof(HabitacionID))]
        public Habitacion? Habitacion { get; set; }
    }
}
