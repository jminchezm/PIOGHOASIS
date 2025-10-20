using PIOGHOASIS.Models.Entities;
using System.ComponentModel.DataAnnotations.Schema;

namespace PIOGHOASIS.Models
{

    [Table("RESERVA", Schema = "dbo")]
    public class Reserva
    {

        public int ReservaID { get; set; }
        public string ClienteID { get; set; } = null!;

        //[Required, StringLength(10)]
        [Column("UsuarioCrea")]
        public string UsuarioID { get; set; } = null!;   // <-- NUEVO: FK a USUARIO
        //public string usuarioIDNombre {  get; set; } = null!;

        //[Column("UsuarioFinaliza")]
        public string? UsuarioFinaliza { get; set; }
        //public string? usuarioFinalizaNombre { get; set; } = null!;

        [Column("EstadoReservaID")]
        public int EstadoReservaID { get; set; }    // cat. EstadoReserva
        public DateTime FechaCheckIn { get; set; }  // 00:00 del día
        public DateTime FechaCheckOut { get; set; } // 00:00 del día siguiente
        public decimal Subtotal { get; set; }
        public decimal Impuestos { get; set; }
        public decimal Total { get; set; }
        public string Codigo { get; set; } = "";    // RES0000001, etc.
        
        public string? NotaCancelacion {  get; set; }

        public Cliente Cliente { get; set; } = null!;
        public EstadoReserva Estado { get; set; } = null!;
        public Usuario Usuario { get; set; } = null!;    // <-- NUEVO: navegación al usuario
        public ICollection<DetalleReserva> Detalles { get; set; } = new List<DetalleReserva>();
        public ICollection<PagoReserva> Pagos { get; set; } = new List<PagoReserva>();

    }
}
