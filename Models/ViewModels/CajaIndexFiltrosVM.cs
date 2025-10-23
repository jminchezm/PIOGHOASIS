namespace PIOGHOASIS.Models.ViewModels
{
    //public class CajaIndexFiltrosVM
    //{
    //    public string? Codigo { get; set; }
    //    public string? Usuario { get; set; }
    //    public short? EstadoCajaID { get; set; } // null = todos
    //}


    public class CajaIndexFiltrosVM
    {
        public string? Codigo { get; set; }
        public string? Usuario { get; set; }
        public short? EstadoCajaID { get; set; } // null = todos

        // 🔽 nuevos
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
    }

    public class CajaIndexItemVM
    {
        public int CajaID { get; set; }
        public string Codigo { get; set; } = default!;
        public string Usuario { get; set; } = default!;
        public DateTime FechaApertura { get; set; }
        public decimal MontoApertura { get; set; }
        public DateTime? FechaCierre { get; set; }
        public string Estado { get; set; } = default!;
    }

    public class CajaIndexVM
    {
        public CajaIndexFiltrosVM Filtros { get; set; } = new();
        public List<CajaIndexItemVM> Items { get; set; } = new();
        public IEnumerable<EstadoCaja> Estados { get; set; } = new List<EstadoCaja>();
    }

    //public class CajaIndexItemVM
    //{
    //    public int CajaID { get; set; }
    //    public string Codigo { get; set; } = default!;
    //    public string Usuario { get; set; } = default!;
    //    public DateTime FechaApertura { get; set; }
    //    public decimal MontoApertura { get; set; }
    //    public DateTime? FechaCierre { get; set; }
    //    public string Estado { get; set; } = default!;
    //}

    //public class CajaIndexVM
    //{
    //    public CajaIndexFiltrosVM Filtros { get; set; } = new();
    //    public List<CajaIndexItemVM> Items { get; set; } = new();
    //    public IEnumerable<EstadoCaja> Estados { get; set; } = new List<EstadoCaja>();
    //}

    public class NuevaCajaVM
    {
        public string Codigo { get; set; } = default!;
        public DateTime Fecha { get; set; } = DateTime.Now;

        // <-- NUEVO
        public string UsuarioId { get; set; } = default!;

        public string Usuario { get; set; } = default!;

        //[Required(ErrorMessage = "Campo obligatorio.")]
        public decimal MontoApertura { get; set; } = 0m;
    }

    //public class NuevaCajaVM
    //{
    //    public string Codigo { get; set; } = default!;
    //    public DateTime Fecha { get; set; } = DateTime.Now;
    //    public string codigoUsuario { get; set; } = default!;
    //    public string Usuario { get; set; } = default!;
    //    public decimal MontoApertura { get; set; } = 0m;
    //}

    public class PagoLineaVM
    {
        public int Nro { get; set; }
        public DateTime Fecha { get; set; }
        public string Cliente { get; set; } = "";
        public decimal Monto { get; set; }

        // NUEVO
        public int? ReservaID { get; set; }
        public string? ReservaCodigo { get; set; }   // si tu entidad Reserva tiene Código/Numero
    }
    //public class PagoLineaVM
    //{
    //    public int Nro { get; set; }
    //    public DateTime Fecha { get; set; }
    //    public string Cliente { get; set; } = "";
    //    public decimal Monto { get; set; }
    //}

    public class AjusteLineaVM
    {
        public int CajaAjusteID { get; set; }
        public DateTime Fecha { get; set; }
        public string TipoTxt { get; set; } = "";     // "Ingreso"/"Egreso"
        public string Motivo { get; set; } = "";
        public decimal Monto { get; set; }
    }

    //public class ResumenMedioVM
    //{
    //    public decimal Efectivo { get; set; }
    //    public decimal Transferencia { get; set; }
    //    public decimal Deposito { get; set; }
    //    public decimal CompraClick { get; set; }
    //    public decimal PlataformaReservas { get; set; }
    //    public decimal Total => Efectivo + Transferencia + Deposito + CompraClick + PlataformaReservas;
    //}

    public class ResumenMedioVM
    {
        public decimal Efectivo { get; set; }
        public decimal Transferencia { get; set; }
        public decimal Deposito { get; set; }
        public decimal CompraClick { get; set; }
        public decimal PlataformaReservas { get; set; }

        // totales actuales
        public decimal Total => Efectivo + Transferencia + Deposito + CompraClick + PlataformaReservas;

        // ajustes
        public decimal AjustesIngreso { get; set; }
        public decimal AjustesEgreso { get; set; }
        public decimal AjustesNetos => AjustesIngreso - AjustesEgreso;
    }

    public class DetalleCajaVM
    {
        public int CajaID { get; set; }
        public string Codigo { get; set; } = default!;
        public DateTime FechaApertura { get; set; }
        public DateTime? FechaCierre { get; set; }
        public string UsuarioAperturaID { get; set; } = default!;
        public string? UsuarioCierreID { get; set; }
        public decimal MontoApertura { get; set; }

        public ResumenMedioVM Resumen { get; set; } = new();
        public List<PagoLineaVM> Pagos { get; set; } = new();

        // ajustes listados
        public List<AjusteLineaVM> Ajustes { get; set; } = new();

        public bool EstaCerrada => FechaCierre.HasValue;

        // Totales mostrados
        public decimal TotalIngresos => Resumen.Total;
        public decimal TotalConApertura => MontoApertura + Resumen.Total;
        public decimal TotalFinal => MontoApertura + Resumen.Total + Resumen.AjustesNetos;
    }

    //public class DetalleCajaVM
    //{
    //    public int CajaID { get; set; }
    //    public string Codigo { get; set; } = default!;
    //    public DateTime FechaApertura { get; set; }
    //    public DateTime? FechaCierre { get; set; }
    //    public string UsuarioAperturaID { get; set; } = default!;
    //    public string? UsuarioCierreID { get; set; }
    //    public decimal MontoApertura { get; set; }
    //    public ResumenMedioVM Resumen { get; set; } = new();
    //    public List<PagoLineaVM> Pagos { get; set; } = new();
    //    public bool EstaCerrada => FechaCierre.HasValue;
    //}
}
