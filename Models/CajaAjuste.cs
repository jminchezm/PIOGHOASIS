using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PIOGHOASIS.Models
{
    [Table("CAJA_AJUSTE", Schema = "dbo")]
    public class CajaAjuste
    {
        public int CajaAjusteID { get; set; }

        public int CajaID { get; set; }
        public Caja? Caja { get; set; }

        // 1 = Ingreso (+), 2 = Egreso (-)
        public short Tipo { get; set; }

        [Range(0.01, double.MaxValue)]
        public decimal Monto { get; set; }

        [Required, StringLength(200)]
        public string Motivo { get; set; } = default!;

        public DateTime Fecha { get; set; } = DateTime.Now;

        [Required, StringLength(10)]
        public string UsuarioID { get; set; } = default!;
    }
}
