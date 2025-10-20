namespace PIOGHOASIS.Models.ViewModels
{
    public class ReporteClientesVM
    {
        // Filtros
        public DateTime? Desde { get; set; }
        public DateTime? Hasta { get; set; }
        public bool SoloActivos { get; set; }

        // KPIs
        public int TotalClientes { get; set; }
        public int NuevosEnRango { get; set; }
        public int ClientesConReserva { get; set; }
        public decimal TicketPromedioCliente { get; set; }    // Q por cliente con pagos en el rango
        public int Repetidores { get; set; }                   // clientes con 2+ reservas en el rango
        public int PaisTopCount { get; set; }                  // únicos países (métrica rápida)

        // Bloques de datos para gráficas
        public List<ItemClaveCount> PorPais { get; set; } = new();
        public List<ItemClaveCount> PorDepartamento { get; set; } = new(); // GTM
        public List<ItemClaveCount> Edades { get; set; } = new();           // buckets
        public List<ItemFechaCount> NuevosPorMes { get; set; } = new();     // altas por mes
        public List<TopClienteRow> TopClientes { get; set; } = new();       // top por gasto

        // Detalle
        public List<ClienteDetalleRow> Detalle { get; set; } = new();
    }

    public record ItemClaveCount(string Clave, int Conteo);
    public record ItemFechaCount(DateTime Fecha, int Conteo);

    public record TopClienteRow(
        string ClienteID,
        string Nombre,
        string? DPI,
        decimal TotalPagado,
        int Reservas,
        DateTime? UltimaVisita
    );

    public record ClienteDetalleRow(
        string ClienteID,
        string Nombre,
        string? DPI,
        string? PaisID,
        DateTime? FechaRegistro,
        int Reservas,
        decimal TotalPagado,
        DateTime? UltimaVisita
    );
}
