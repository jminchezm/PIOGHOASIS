namespace PIOGHOASIS.Models.ViewModels
{
    public class ReporteIngresosVM
    {
        // Filtros
        public DateTime? Desde { get; set; }
        public DateTime? Hasta { get; set; }
        public short? TipoPagoId { get; set; }
        public short? FormaPagoId { get; set; }
        public short? PlataformaId { get; set; }

        // KPIs
        public decimal TotalCobrado { get; set; }
        public int CantPagos { get; set; }
        public decimal TicketPromedio { get; set; }

        // Tabla principal
        public List<PagoRowIng> Pagos { get; set; } = new();

        // Dashboards
        public List<ItemMonto> PorTipoPago { get; set; } = new();
        public List<ItemMonto> PorFormaPago { get; set; } = new();
        public List<ItemMonto> PorPlataforma { get; set; } = new();
        public List<ItemMontoDate> PorDia { get; set; } = new();
    }

    public record PagoRowIng(
        DateTime FechaPago,
        string TipoPago,
        string FormaPago,
        string? Plataforma,
        string ReservaCodigo,
        string Cliente,
        decimal Monto,
        string? NumeroReferencia
    );
}



//namespace PIOGHOASIS.Models.ViewModels
//{
//    public class ReporteIngresosVM
//    {
//        // ===== Filtros =====
//        public DateTime? Desde { get; set; }
//        public DateTime? Hasta { get; set; }
//        public short? TipoPagoId { get; set; }
//        public short? FormaPagoId { get; set; }
//        public short? PlataformaId { get; set; }

//        // ===== KPIs =====
//        public decimal TotalCobrado { get; set; }
//        public int CantPagos { get; set; }
//        public decimal TicketPromedio { get; set; }

//        // ===== Tablas =====
//        public List<PagoRoww> Pagos { get; set; } = new();

//        // ===== Acumulados para dashboards =====
//        public List<ItemMonto> PorTipoPago { get; set; } = new();
//        public List<ItemMonto> PorFormaPago { get; set; } = new();
//        public List<ItemMonto> PorPlataforma { get; set; } = new();
//        public List<ItemMontoDate> PorDia { get; set; } = new();
//    }

//    public record ItemMonto(string Clave, decimal Monto);
//    public record ItemMontoDate(DateTime Dia, decimal Monto);

//    public record PagoRow(
//        DateTime FechaPago,
//        string TipoPago,
//        string FormaPago,
//        string? Plataforma,
//        string ReservaCodigo,
//        string Cliente,
//        decimal Monto,
//        string? NumeroReferencia
//    );
//}
