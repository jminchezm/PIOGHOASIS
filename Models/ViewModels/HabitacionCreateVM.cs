using System.ComponentModel.DataAnnotations;

namespace PIOGHOASIS.Models.ViewModels
{
    public class HabitacionCreateVM
    {
        public Habitacion Habitacion { get; set; } = new Habitacion();

        // Lo usaremos para binder de listas con índices: TarifaItems[0].NumeroPersonas ...
        public List<TarifaItemVM> TarifaItems { get; set; } = new();
    }

    public class TarifaItemVM
    {
        [Range(1, 99, ErrorMessage = "Ocupación inválida")]
        public int NumeroPersonas { get; set; }

        // Usaremos string para aceptar “1,234.50” y luego normalizar a decimal en el POST
        [Required] public string PrecioNocheStr { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        public DateTime FechaInicio { get; set; }

        [DataType(DataType.Date)]
        public DateTime FechaFin { get; set; }

        public string? EtiquetaTemporada { get; set; }
    }
}
