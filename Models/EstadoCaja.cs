using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PIOGHOASIS.Models
{
    [Table("ESTADO_CAJA", Schema = "dbo")]
    public class EstadoCaja
    {
        public short EstadoCajaID { get; set; }

        [Required, StringLength(10)]
        public string Codigo { get; set; } = default!;
        [Required, StringLength(30)]
        public string Nombre { get; set; } = default!;

        public bool Estado { get; set; } = true;
    }
}
