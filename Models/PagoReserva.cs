using System.ComponentModel.DataAnnotations;

namespace PIOGHOASIS.Models
{

    public enum MedioPago
    {
        Efectivo = 1,
        Transferencia = 2,
        Deposito = 3,
        CompraClick = 4,
        PlataformaReservas = 5
    }

    // Models/PagoReserva.cs
    public class PagoReserva
    {
        public int PagoReservaID { get; set; }

        public int ReservaID { get; set; }
        public Reserva Reserva { get; set; } = null!;

        [Required(ErrorMessage = "El campo Persona es obligatorio.")]
        public short FormaPagoID { get; set; }         // 1=Completo, 2=Anticipo
        public FormaPago FormaPago { get; set; } = null!;

        [Required(ErrorMessage = "El campo Persona es obligatorio.")]
        public short TipoPagoID { get; set; }          // Efectivo/Transferencia/Depósito/Plataforma, etc.
        public TipoPago TipoPago { get; set; } = null!;

        public short? PlataformaID { get; set; }       // WhatsApp/Booking/Expedia/Presencial...
        public PlataformaReserva? Plataforma { get; set; }

        [Required(ErrorMessage = "El campo Persona es obligatorio.")]
        public DateTime FechaPago { get; set; }

        public string? NumeroReferencia { get; set; }  // requerido salvo Efectivo

        [Required(ErrorMessage = "El campo Persona es obligatorio.")]
        public decimal MontoPagado { get; set; }
        public string? Observaciones { get; set; }

        // Archivo (opción simple)
        public string? ComprobantePath { get; set; }   // p.ej. "/uploads/pagos/RES000012/2025-05-20_abc.pdf"
        public string? ComprobanteNombre { get; set; } // nombre original
        public string? ComprobanteMime { get; set; }   // "application/pdf", "image/jpeg", etc.

        //public DateTime CreadoEl { get; set; } = DateTime.UtcNow;
    }
}
