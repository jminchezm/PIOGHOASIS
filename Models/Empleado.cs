using Microsoft.EntityFrameworkCore;
using PIOGHOASIS.Models.Entities;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace PIOGHOASIS.Models
{
    [PrimaryKey(nameof(EmpleadoID))]
    [Table("EMPLEADO", Schema = "dbo")]
    public class Empleado
    {
        [Column("EmpleadoID"), StringLength(10)]
        public string EmpleadoID { get; set; } = null!;

        [Column("PersonaID"), StringLength(10)]
        //[ForeignKey(nameof(Puesto))]
        public string PersonalID { get; set; } = null!;

        [Column("PuestoID"), StringLength(10)]
        //[ForeignKey(nameof(Puesto))]
        public string PuestoID { get; set; } = null!;

        [Column("FechaContratacion")]
        public DateTime? FechaContratacion { get; set; }

        [Column("Estado")]
        public bool Estado { get; set; }

        // Navegación
        public Persona Persona { get; set; } = null!;
        public Usuario? Usuario { get; set; } // 1-1 con Usuario
        public Puesto Puesto { get; set; } = null!;
    }
}
