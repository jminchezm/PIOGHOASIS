namespace PIOGHOASIS.Models.ViewModels
{
    public class BusquedaHabitacionVM
    {
        public DateTime? CheckIn { get; set; }
        public DateTime? CheckOut { get; set; }
        public int Personas { get; set; } = 1;
        public List<HabitacionDisponibleVM> Resultados { get; set; } = new();
    }

    public class HabitacionDisponibleVM
    {
        public int HabitacionID { get; set; }
        public string Codigo { get; set; } = "";
        public string? Imagen { get; set; }
        public string Titulo { get; set; } = "";
        public string TipoNombre { get; set; } = "";
        public string NumeroHabitacion { get; set; } = "";
        public int Capacidad { get; set; }

        // precio “base” mostrado actualmente
        public decimal PrecioNoche { get; set; }

        // NUEVO – para cambiar tarifa/personas en la tarjeta
        public int PersonasSeleccionadas { get; set; } = 1;
        public int? TarifaSeleccionadaID { get; set; }
        public decimal TotalConImpuestos { get; set; }   // precio+noche con impuestos

        public List<TarifaOpcionVM> Tarifas { get; set; } = new();

        // NUEVO: personas para las que SÍ hay tarifa
        public List<int> PersonasDisponibles { get; set; } = new();
    }

    public class TarifaOpcionVM
    {
        public int Personas { get; set; }
        public int? TarifaID { get; set; }
        public decimal PrecioNoche { get; set; }
        public decimal TotalConImpuestos { get; set; }

        // Desglose POR NOCHE (opcional, por si quieres mostrarlo en algún lado)
        public decimal BaseSinImpuestos { get; set; }
        public decimal Inguat { get; set; }
        public decimal Iva { get; set; }

        public string? Etiqueta { get; set; }
    }



    //public class HabitacionDisponibleVM
    //{
    //    public int HabitacionID { get; set; }
    //    public string Codigo { get; set; } = "";
    //    public string? Imagen { get; set; }
    //    public string Titulo { get; set; } = ""; // ej. "Standard #1"
    //    public string TipoNombre { get; set; } = "";    // ej. "Standard"
    //    public string NumeroHabitacion { get; set; } = ""; // ej. "1"
    //    public int Capacidad { get; set; }
    //    public decimal PrecioNoche { get; set; }

    //    //public string nombreTipoHabitacion {  get; set; } = "";
    //}

    public class ReservaResumenVM
    {
        public DateTime CheckIn { get; set; }
        public DateTime CheckOut { get; set; }
        public int Noches { get; set; }
        public int Personas { get; set; }

        public int HabitacionID { get; set; }
        public string HabitacionTitulo { get; set; } = "";
        public decimal PrecioNoche { get; set; }
        public int? TarifaID { get; set; }

        public decimal Subtotal { get; set; }
        public decimal Impuestos { get; set; }
        public decimal Total { get; set; }

        // Cliente seleccionado
        public string? ClienteID { get; set; }
        public string? ClienteNombre { get; set; }

        // === NUEVO: guardar precio de lista / original ===
        public decimal PrecioNocheOriginal { get; set; }

        public bool TieneDescuento =>
            PrecioNocheOriginal > 0 && PrecioNoche < PrecioNocheOriginal;

        public decimal DescuentoPorNoche =>
            (PrecioNocheOriginal > 0 && PrecioNocheOriginal > PrecioNoche)
                ? (PrecioNocheOriginal - PrecioNoche)
                : 0m;

        public decimal DescuentoTotal =>
            DescuentoPorNoche * Noches;
    }

    public class ReservaCreateVM
    {
        public ReservaResumenVM Resumen { get; set; } = new();
        public IEnumerable<Cliente>? Clientes { get; set; }
    }
}
