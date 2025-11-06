using Microsoft.AspNetCore.Mvc.Rendering;

namespace PIOGHOASIS.Models.ViewModels
{
    public class ReporteReservasVM
    {
        // Filtros
        public DateTime? Desde { get; set; }
        public DateTime? Hasta { get; set; }

        // Nuevo: filtro por estado (código o nombre) y habitación
        public string? EstadoSeleccionado { get; set; }
        public int? HabitacionSeleccionadaId { get; set; }

        public IEnumerable<SelectListItem> Estados { get; set; } = Enumerable.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> Habitaciones { get; set; } = Enumerable.Empty<SelectListItem>();

        // KPIs NUEVOS
        public int NumReservasReservadas { get; set; }
        public int NumReservasConfirmadas { get; set; }
        public int NumReservasCanceladas { get; set; }
        public decimal TotalCobrado { get; set; }

        // KPIs antiguos (los dejamos por si los reutilizas en otro lado)
        public int LlegadasHoy { get; set; }
        public int SalidasHoy { get; set; }
        public decimal Ocupacion { get; set; }
        public decimal ADR { get; set; }
        public decimal RevPAR { get; set; }
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
