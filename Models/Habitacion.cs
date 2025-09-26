using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PIOGHOASIS.Models
{
    [Table("HABITACION")]
    public class Habitacion
    {

        [Key]
        public int HabitacionID { get; set; } // PK (Identity en DB)

        [Required(ErrorMessage = "Campo obligatorio.") ,StringLength(10)]
        public string Codigo { get; set; } = string.Empty; // HAB001…

        [Required(ErrorMessage = "Campo obligatorio"), StringLength(10)]
        public string NumeroHabitacion { get; set; } = string.Empty;

        [Required(ErrorMessage = "Campo obligatorio"), StringLength(20)]
        public string Piso { get; set; } = string.Empty;

        [Required(ErrorMessage = "Campo obligatorio"), StringLength(10)]
        public string TipoHabitacionID { get; set; } = string.Empty; // FK

        [Required(ErrorMessage = "Campo obligatorio"), Range(1, 99, ErrorMessage = "El campo debe ser mayor a 0")]
        public short? CapacidadPersonas { get; set; }

        [Required(ErrorMessage = "Campo obligatorio"), StringLength(100)]
        public string DescripcionCamas { get; set; } = string.Empty;

        [Required(ErrorMessage = "Campo obligatorio"), StringLength(300)]
        public string Descripcion { get; set; } = string.Empty;
        public bool? AireAcondicionado { get; set; }
        public bool? Ventilador { get; set; }
        public bool? TV { get; set; }
        public bool? BanoPrivado { get; set; }
        public bool? WIFI { get; set; }
        public bool? Parqueo { get; set; }

        [Required(ErrorMessage = "El campo Estado es obligatorio.")]
        public bool Estado { get; set; } = true; // Disponible/Activo

        [StringLength(200)]
        public string? Imagen { get; set; } // ruta relativa /uploads/habs/xxx.jpg

        // NAV
        [ForeignKey(nameof(TipoHabitacionID))]
        public TipoHabitacion? TipoHabitacion { get; set; }

    }
}
