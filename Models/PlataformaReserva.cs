using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PIOGHOASIS.Models
{
    public class PlataformaReserva
    {

        [Key]                           // <- clave primaria
        [Column("PlataformaID")]
        public short PlataformaID { get; set; }
        public string Codigo { get; set; } = "";
        public string Nombre { get; set; } = "";
        public bool Estado { get; set; } = true;

    }
}
