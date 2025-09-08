using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PIOGHOASIS.Models
{
    [PrimaryKey(nameof(MunicipioID))]
    [Table("MUNICIPIO", Schema = "dbo")]
    public class Municipio
    {
        [Column("MunicipioID")]
        public int MunicipioID { get; set; }

        [Column("Nombre"), StringLength(100)]
        public string Nombre { get; set; } = null!;

        [Column("Estado")]
        public bool Estado { get; set; }

        [Column("DepartamentoID")]
        public int DepartamentoID { get; set; }

        // Navegación
        public Departamento Departamento { get; set; } = null!;
    }
}
