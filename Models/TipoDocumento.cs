using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PIOGHOASIS.Models
{
    [PrimaryKey(nameof(TipoDocumentoID))]
    [Table("TIPO_DOCUMENTO", Schema = "dbo")]
    public class TipoDocumento
    {
        [Column("TipoDocumentoID"), StringLength(10)]
        public string TipoDocumentoID { get; set; } = null!;

        [Column("Nombre"), StringLength(100)]
        [Required(ErrorMessage = "El campo Nombre es obligatorio.")]
        public string Nombre { get; set; } = null!;

        //[Column("Descripcion"), StringLength(300)]
        //public string? Descripcion { get; set; }

        [Column("Estado")]
        public bool Estado { get; set; }
    }
}
