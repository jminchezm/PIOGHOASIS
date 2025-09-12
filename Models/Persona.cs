using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PIOGHOASIS.Models
{
    [PrimaryKey(nameof(PersonaID))]
    [Table("PERSONA", Schema = "dbo")]
    public class Persona
    {
        [Column("PersonaID"), StringLength(10)]
        public string PersonaID { get; set; } = null!;

        [Column("PrimerNombre"), StringLength(50)]
        [Required(ErrorMessage = "El campo Primer Nombre es obligatorio.")]
        public string PrimerNombre { get; set; } = null!;

        [Column("SegundoNombre"), StringLength(50)]
        public string? SegundoNombre { get; set; }

        [Column("PrimerApellido"), StringLength(50)]
        [Required(ErrorMessage = "El campo Primer Apellido es obligatorio.")]
        public string PrimerApellido { get; set; } = null!;

        [Column("SegundoApellido"), StringLength(50)]
        public string? SegundoApellido { get; set; }

        [Column("ApellidoCasada"), StringLength(50)]
        public string? ApellidoCasada { get; set; }

        [Column("Email"), StringLength(100)]
        public string? Email { get; set; }

        [RegularExpression(@"^\d{8}$", ErrorMessage = "El Teléfono 1 debe tener 8 dígitos.")]
        [StringLength(8)]
        [Required(ErrorMessage = "El campo Teléfono 1 es obligatorio.")]
        public string? Telefono1 { get; set; }

        [Column("Telefono2"), StringLength(8)]
        public string? Telefono2 { get; set; }

        [Column("Direccion"), StringLength(250)]
        [Required(ErrorMessage = "El campo Dirección es obligatorio.")]
        public string? Direccion { get; set; }

        [Column("TipoDocumentoID"), StringLength(10)]
        [Required(ErrorMessage = "El campo Tipo Documento es obligatorio.")]
        public string? TipoDocumentoID { get; set; }

        [Column("NumeroDocumento"), StringLength(20)]
        [Required(ErrorMessage = "El campo Número Documento es obligatorio.")]
        public string? NumeroDocumento { get; set; }

        [Column("Nit"), StringLength(15)]
        public string? Nit { get; set; }

        [Column("FechaNacimiento")]
        [Required(ErrorMessage = "El campo Fecha Nacimiento es obligatorio.")]
        public DateTime? FechaNacimiento { get; set; }

        [Column("PaisID"), StringLength(10)]
        [Required(ErrorMessage = "El campo Pais es obligatorio.")]
        public string? PaisID { get; set; }

        [Column("DepartamentoID")]
        //[Required(ErrorMessage = "El campo Departamento es obligatorio.")]
        public int? DepartamentoID { get; set; }

        [Column("MunicipioID")]
        //[Required(ErrorMessage = "El campo Municipio es obligatorio.")]
        public int? MunicipioID { get; set; }

        [Column("FechaRegistro")]
        [Required(ErrorMessage = "El campo Fecha Registro es obligatorio.")]
        public DateTime? FechaRegistro { get; set; }

        [Column("FotoPath")]              // RUTA relativa bajo wwwroot (ej: "uploads/empleados/USR1.jpg")
        public string? FotoPath { get; set; }

        // Navegación 1-1 con Empleado
        public Empleado? Empleado { get; set; }
    }
}
