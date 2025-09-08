using Microsoft.EntityFrameworkCore;
using PIOGHOASIS.Models.Entities;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace PIOGHOASIS.Models
{
    [PrimaryKey(nameof(RolID))]
    [Table("ROL", Schema = "dbo")]
    public class Rol
    {
        [Column("RolID"), StringLength(10)]
        public string RolID { get; set; } = null!;

        [Column("Nombre"), StringLength(100)]
        [Required(ErrorMessage = "El campo Nombre es obligatorio.")]
        public string Nombre { get; set; } = null!;

        [Column("Descripcion"), StringLength(300)]
        public string? Descripcion { get; set; }

        [Column("Estado")]
        [Required]
        public bool Estado { get; set; }

        // navegación inversa
        public ICollection<Usuario> Usuarios { get; set; } = new List<Usuario>();
    }
}
