using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PIOGHOASIS.Models
{
    [Table("ESTADO_RESERVA", Schema = "dbo")]  // 👈 nombre real en BD
    public class EstadoReserva
    {
        [Key]
        [Column("EstadoReservaID")]
        public int EstadoReservaID { get; set; }   // p.ej. 1=Reservada, 2=Confirmada, 3=Cancelada
        public string Codigo { get; set; } = "";
        public string Nombre { get; set; } = "";
        public bool Estado { get; set; } = true;
    }
}
