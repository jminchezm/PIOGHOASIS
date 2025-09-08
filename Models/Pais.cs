using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PIOGHOASIS.Models
{
    [PrimaryKey(nameof(PaisID))]
    [Table("PAIS", Schema = "dbo")]
    public class Pais
    {
        [Column("PaisID"), StringLength(10)]
        public string PaisID { get; set; } = null!;

        [Column("CodigoNumerico")]
        public int CodigoNumerico { get; set; }

        [Column("Nombre"), StringLength(100)]
        public string Nombre { get; set; } = null!;

        [Column("Estado")]
        public bool Estado { get; set; }

        // Navegación
        public ICollection<Departamento> Departamentos { get; set; } = new List<Departamento>();
    }
}
