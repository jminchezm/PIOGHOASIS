using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PIOGHOASIS.Models.Entities
{
    [PrimaryKey(nameof(UsuarioID))]
    [Table("USUARIO", Schema = "dbo")]

    public class Usuario
    {
        // --- columnas mapeadas (tal cual las tienes) ---
        public string UsuarioID { get; set; } = null!;
        [Required(ErrorMessage = "El campo Nombre de Usuario es obligatorio")]
        public string UsuarioNombre { get; set; } = null!;
        [BindNever]
        [ValidateNever]
        [Column("Contrasena")]
        public byte[]? Contrasena { get; set; }
        [Required(ErrorMessage = "El campo Empleado es obligatorio")]
        public string? EmpleadoID { get; set; }
        [StringLength(10)]
        [Required(ErrorMessage = "El campo Rol es obligatorio")]
        public string? RolID { get; set; }
        [Required(ErrorMessage = "El campo Estado es obligatorio")]
        public bool Estado { get; set; } = true;
        public DateTime FechaRegistro { get; set; }
        public Empleado Empleado { get; set; } = null!;
        public Rol? Rol { get; set; }

        // --- NO mapeadas: sólo para el formulario ---
        [NotMapped]
        [Display(Name = "Contraseña")]
        //[Required(ErrorMessage = "La contraseña es obligatoria.")]
        [StringLength(15, MinimumLength = 8, ErrorMessage = "Debe tener entre 8 y 15 caracteres.")]
        [RegularExpression(@"(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+",
            ErrorMessage = "Debe incluir al menos una minúscula, una mayúscula y un número.")]
        public string? NuevaContrasena { get; set; }

        [NotMapped]
        [Display(Name = "Confirmar Contraseña")]
        //[Required(ErrorMessage = "Confirma la contraseña.")]
        [Compare(nameof(NuevaContrasena), ErrorMessage = "La confirmación no coincide.")]
        public string? ConfirmarContrasena { get; set; }
    }
}
