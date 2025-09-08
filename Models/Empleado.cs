using Microsoft.EntityFrameworkCore;
using PIOGHOASIS.Models.Entities;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace PIOGHOASIS.Models
{
    [PrimaryKey(nameof(EmpleadoID))]
    [Table("EMPLEADO", Schema = "dbo")]
    public class Empleado
    {
        [Column("EmpleadoID"), StringLength(10)]
        public string EmpleadoID { get; set; } = null!;

        [Column("PersonaID"), StringLength(10)]
        [Required(ErrorMessage = "El campo Persona es obligatorio.")]
        //[ForeignKey(nameof(Puesto))]
        public string PersonalID { get; set; } = null!;

        [Column("PuestoID"), StringLength(10)]
        [Required(ErrorMessage = "El campo Puesto es obligatorio.")]
        //[ForeignKey(nameof(Puesto))]
        public string PuestoID { get; set; } = null!;

        [Column("FechaContratacion")]
        public DateTime? FechaContratacion { get; set; }

        [Column("Estado")]
        [Required(ErrorMessage = "El campo Estado es obligatorio.")]
        public bool Estado { get; set; }

        // Navegación
        [ValidateNever]  public Persona Persona { get; set; } = null!;
        public Usuario? Usuario { get; set; } // 1-1 con Usuario
        [ValidateNever]  public Puesto Puesto { get; set; } = null!;
    }
}
