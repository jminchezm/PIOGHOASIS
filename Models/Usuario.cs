using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PIOGHOASIS.Models.Entities
{
    [PrimaryKey(nameof(UsuarioID))]
    [Table("USUARIO", Schema = "dbo")]
    public class Usuario
    {
        [Column("UsuarioID")]
        public string UsuarioID { get; set; } = null!;

        [Column("UsuarioNombre")]
        public string UsuarioNombre { get; set; } = null!;

        // varbinary(64) en SQL Server -> byte[] en C#
        [Column("Contrasena")]
        public byte[] Contrasena { get; set; } = null!;

        [Column("EmpleadoID")]
        public string? EmpleadoID { get; set; }

        [Column("RolID"), StringLength(10)]
        public string? RolID { get; set; }

        [Column("Estado")]
        public bool Estado { get; set; } = true; // 1=activo, 0=inactivo

        [Column("FechaRegistro")]
        public DateTime FechaRegistro { get; set; }

        // Navegación
        public Empleado Empleado { get; set; } = null!;
        public Rol? Rol { get; set; }    // navegación al rol
    }
}
