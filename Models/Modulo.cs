using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PIOGHOASIS.Models
{
    [Table("MODULO")]
    public class Modulo
    {
        [Key]
        public int ModuloID { get; set; }                 // INT (Identity en DB)

        [Required, StringLength(10)]
        public string Codigo { get; set; } = default!;    // NVARCHAR(20) NOT NULL

        [Required(ErrorMessage = "El campo es obligatorio."), StringLength(120)]
        public string Nombre { get; set; } = default!;    // NVARCHAR(120) NOT NULL

        [StringLength(400)]
        public string? Descripcion { get; set; }          // NVARCHAR(400) NULL

        [Required]
        public bool Estado { get; set; } = true;          // BIT NOT NULL

        [Required]
        public DateTime FechaRegistro { get; set; } = DateTime.Now; // DATETIME2(0) NOT NULL
    }
}
