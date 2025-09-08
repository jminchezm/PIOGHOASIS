using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PIOGHOASIS.Models
{
    [PrimaryKey(nameof(DepartamentoID))]
    [Table("DEPARTAMENTO", Schema = "dbo")]
    public class Departamento
    {
        [Column("DepartamentoID")]
        public int DepartamentoID { get; set; }

        [Column("Nombre"), StringLength(100)]
        public string Nombre { get; set; } = null!;

        [Column("Estado")]
        public bool Estado { get; set; }

        [Column("PaisID"), StringLength(10)]
        public string PaisID { get; set; } = null!;

        // Navegación
        public Pais Pais { get; set; } = null!;
    }
}
