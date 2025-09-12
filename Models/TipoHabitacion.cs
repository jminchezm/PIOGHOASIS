using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace PIOGHOASIS.Models
{
    //[Table("TIPO_HABITACION")]
    public class TipoHabitacion
    {
        [Key, StringLength(10)]
        public string TipoHabitacionID { get; set; } = "";

        [StringLength(100)]
        [Required(ErrorMessage = "El campo Nombre es obligatorio.")]
        public string Nombre { get; set; } = "";

        [StringLength(300)]
        public string? Descripcion { get; set; }

        public bool Estado { get; set; }
    }
}
