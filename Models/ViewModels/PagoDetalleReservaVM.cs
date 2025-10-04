namespace PIOGHOASIS.Models.ViewModels
{
    public class PagoDetalleReservaVM
    {
        public Reserva Reserva { get; set; } = null!;
        public List<PagoReserva> Pagos { get; set; } = new();
        public PagoReserva? Seleccionado { get; set; } // último o el elegido
        public decimal Pagado { get; set; }
        public decimal Pendiente { get; set; }
    }
}
