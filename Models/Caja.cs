using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PIOGHOASIS.Models
{
    [Table("CAJA", Schema = "dbo")]
    public class Caja
    {
        public int CajaID { get; set; }

        [Required, StringLength(20)]
        public string Codigo { get; set; } = default!;

        public DateTime FechaApertura { get; set; }
        [Required, StringLength(10)]
        public string UsuarioAperturaID { get; set; } = default!;

        //[Required(ErrorMessage = "Campo obligatorio.")]
        public decimal MontoApertura { get; set; }

        public short EstadoCajaID { get; set; }  // FK a ESTADO_CAJA
        public EstadoCaja? EstadoCaja { get; set; }

        public DateTime? FechaCierre { get; set; }
        [StringLength(10)]
        public string? UsuarioCierreID { get; set; }

        [StringLength(300)]
        public string? Observaciones { get; set; }

        public ICollection<CajaPago> CajaPagos { get; set; } = new List<CajaPago>();
        public ICollection<CajaAjuste> Ajustes { get; set; } = new List<CajaAjuste>();
    }
}
