using System.ComponentModel.DataAnnotations.Schema;

namespace PIOGHOASIS.Models
{
    [Table("CAJA_PAGO", Schema = "dbo")]
    public class CajaPago
    {
        public int CajaPagoID { get; set; }

        public int CajaID { get; set; }
        public Caja? Caja { get; set; }

        public int PagoReservaID { get; set; }   // FK lógico a pagos
        public DateTime FechaRegistro { get; set; }
    }
}
