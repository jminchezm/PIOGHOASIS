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
        public string PrimerNombre { get; set; } = null!;

        [Column("SegundoNombre"), StringLength(50)]
        public string? SegundoNombre { get; set; }

        [Column("PrimerApellido"), StringLength(50)]
        public string PrimerApellido { get; set; } = null!;

        [Column("SegundoApellido"), StringLength(50)]
        public string? SegundoApellido { get; set; }

        [Column("ApellidoCasada"), StringLength(50)]
        public string? ApellidoCasada { get; set; }

        [Column("Email"), StringLength(100)]
        public string? Email { get; set; }

        [Column("Telefono1"), StringLength(8)]
        public string? Telefono1 { get; set; }

        [Column("Telefono2"), StringLength(8)]
        public string? Telefono2 { get; set; }

        [Column("Direccion"), StringLength(250)]
        public string? Direccion { get; set; }

        [Column("TipoDocumentoID"), StringLength(10)]
        public string? TipoDocumentoID { get; set; }

        [Column("NumeroDocumento"), StringLength(20)]
        public string? NumeroDocumento { get; set; }

        [Column("Nit"), StringLength(15)]
        public string? Nit { get; set; }

        [Column("FechaNacimiento")]
        public DateTime? FechaNacimiento { get; set; }

        [Column("PaisID"), StringLength(10)]
        public string? PaisID { get; set; }

        [Column("MunicipioID")]
        public int? MunicipioID { get; set; }

        [Column("FechaRegistro")]
        public DateTime? FechaRegistro { get; set; }

        [Column("FotoPath")]              // RUTA relativa bajo wwwroot (ej: "uploads/empleados/USR1.jpg")
        public string? FotoPath { get; set; }

        // Navegación 1-1 con Empleado
        public Empleado? Empleado { get; set; }
    }
}
