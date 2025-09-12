using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PIOGHOASIS.Models
{
    [Table("CLIENTE", Schema = "dbo")]
    public class Cliente
    {
        [Key]
        [Column("ClienteID"), StringLength(10)]
        public string ClienteID { get; set; } = null!;

        [Required]
        [Column("PersonaID"), StringLength(10)]
        public string PersonaID { get; set; } = null!;

        [Column("Estado")]
        public bool Estado { get; set; } = true;

        // Navegación
        [ForeignKey(nameof(PersonaID))]
        [ValidateNever] public Persona Persona { get; set; } = null!;
    }
}
