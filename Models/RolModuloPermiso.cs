using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PIOGHOASIS.Models
{
    [Table("ROL_MODULO_PERMISO")]
    public class RolModuloPermiso
    {
        [Key]
        public int RolModuloPermisoID { get; set; }

        [Required, StringLength(10)]
        public string RolID { get; set; } = string.Empty;

        [Required]
        public int ModuloID { get; set; }

        public bool PuedeAcceder { get; set; }      // usado en la UI (columna única)
        public bool PuedeCrear { get; set; }        // futuros usos
        public bool PuedeEditar { get; set; }
        public bool PuedeEliminar { get; set; }

        public bool Estado { get; set; } = true;
        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        // Navegaciones (asumiendo ya existen estas entidades)
        public Rol? Rol { get; set; }
        public Modulo? Modulo { get; set; }
    }
}
