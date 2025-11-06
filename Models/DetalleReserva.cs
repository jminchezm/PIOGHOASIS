namespace PIOGHOASIS.Models
{
    public class DetalleReserva
    {
        public int DetalleReservaID { get; set; }
        public int ReservaID { get; set; }
        public int HabitacionID { get; set; }

        public int Personas { get; set; }
        public int Noches { get; set; }

        // “Snapshot” de precio aplicado (¡muy importante!)
        public decimal PrecioPorNoche { get; set; }
        public decimal TotalLinea { get; set; }

        // Opcional: si usas recargos por extra
        public int? PersonasExtra { get; set; }
        public decimal? CargoExtra { get; set; }

        // (Opcional) guarda la TarifaID usada para auditoría
        public int? TarifaID { get; set; }

        public decimal? PrecioListaPorNoche { get; set; }
        public decimal? DescuentoPorNoche { get; set; }

        // (opcional) propiedad de conveniencia
        public decimal DescuentoTotalLinea => (DescuentoPorNoche ?? 0m) * Noches;

        public Reserva Reserva { get; set; } = null!;
        public Habitacion Habitacion { get; set; } = null!;
        public TarifaHabitacion? Tarifa { get; set; }
    }
}
