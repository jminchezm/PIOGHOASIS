namespace PIOGHOASIS.Models.ViewModels
{
    public class PagoDetalleReservaVM
    {
        public Reserva Reserva { get; set; } = null!;
        public List<PagoReserva> Pagos { get; set; } = new();
        public PagoReserva? Seleccionado { get; set; } // último o el elegido
        public decimal Pagado { get; set; }
        public decimal Pendiente { get; set; }

        public decimal PrecioListaPorNoche { get; set; }
        public decimal PrecioFinalPorNoche { get; set; }
        public decimal DescuentoPorNoche { get; set; }
        public decimal DescuentoTotal { get; set; }

        // Conveniencia para las vistas
        public bool TieneDescuento => DescuentoTotal > 0;
    }
}
