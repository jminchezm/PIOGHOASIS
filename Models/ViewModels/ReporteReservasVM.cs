namespace PIOGHOASIS.Models.ViewModels
{
    public class ReporteReservasVM
    {
        // Filtros
        public DateTime? Desde { get; set; }
        public DateTime? Hasta { get; set; }
        public int? EstadoId { get; set; }
        public int? TipoHabitacionId { get; set; }
        public short? TipoPagoId { get; set; }
        public short? PlataformaId { get; set; }
        public bool ModoPorPagos { get; set; }

        // KPIs
        public int LlegadasHoy { get; set; }
        public int SalidasHoy { get; set; }
        public decimal Ocupacion { get; set; }
        public decimal ADR { get; set; }
        public decimal RevPAR { get; set; }
        public decimal TotalCobrado { get; set; }
        public decimal CuentasPorCobrar { get; set; }

        // Tablas
        public List<ReservaRow> Reservas { get; set; } = new();
        public List<PagoRowRes> Pagos { get; set; } = new();

        // Acumulados
        public List<ItemMonto> PorTipoPago { get; set; } = new();
        public List<ItemMonto> PorPlataforma { get; set; } = new();
    }

    public record PagoRowRes(DateTime FechaPago, string Tipo, string? Plataforma, decimal Monto, string CodigoReserva);

    public record ReservaRow(
        string Codigo, string Estado, DateTime In, DateTime Out,
        string Cliente, string Hab, decimal Total, decimal Pagado, decimal Pendiente);
}


//namespace PIOGHOASIS.Models.ViewModels
//{
//    public class ReporteReservasVM
//    {
//        // Filtros
//        public DateTime? Desde { get; set; }
//        public DateTime? Hasta { get; set; }
//        public int? EstadoId { get; set; }
//        public int? TipoHabitacionId { get; set; }
//        public short? TipoPagoId { get; set; }
//        public short? PlataformaId { get; set; }
//        public bool ModoPorPagos { get; set; } // true=filtra por pagos, false=por estancia

//        // KPIs
//        public int LlegadasHoy { get; set; }
//        public int SalidasHoy { get; set; }
//        public decimal Ocupacion { get; set; }
//        public decimal ADR { get; set; }
//        public decimal RevPAR { get; set; }
//        public decimal TotalCobrado { get; set; }
//        public decimal CuentasPorCobrar { get; set; }

//        // Tablas
//        public List<ReservaRow> Reservas { get; set; } = new();
//        public List<PagoRoww> Pagos { get; set; } = new();

//        // Acumulados
//        public List<ItemMonto> PorTipoPago { get; set; } = new();
//        public List<ItemMonto> PorPlataforma { get; set; } = new();
//    }

//    public record ItemMonto(string Clave, decimal Monto);

//    public record PagoRoww(DateTime FechaPago, string Tipo, string? Plataforma, decimal Monto, string CodigoReserva);

//    public record ReservaRow(
//        string Codigo, string Estado, DateTime In, DateTime Out,
//        string Cliente, string Hab, decimal Total, decimal Pagado, decimal Pendiente);
//}
