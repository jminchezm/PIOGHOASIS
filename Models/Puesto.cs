using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace PIOGHOASIS.Models
{
    [PrimaryKey(nameof(PuestoID))]
    [Table("PUESTO", Schema = "dbo")]
    public class Puesto
    {
        [Column("PuestoID"), StringLength(10)]
        public string PuestoID { get; set; } = null!;

        [Column("Nombre"), StringLength(100)]
        [Required(ErrorMessage = "El campo Nombre es obligatorio.")]
        public string Nombre { get; set; } = null!;

        [Column("Descripcion"), StringLength(300)]
        public string? Descripcion { get; set; }

        [Column("Estado")]
        [Required(ErrorMessage = "El campo Estado es obligatorio.")]
        public bool Estado { get; set; } = true;  // si en BD el default es 1, puedes dejarlo en true

        // Navegación (opcional pero útil si relacionas con EMPLEADO)
        public ICollection<Empleado> Empleados { get; set; } = new List<Empleado>();
    }
}
