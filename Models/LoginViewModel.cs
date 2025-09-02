using System.ComponentModel.DataAnnotations;

namespace PIOGHOASIS.Models
{
    public class LoginViewModel
    {
        [Required]
        [Display(Name = "Usuario")]
        public string Usuario { get; set; } = null!;

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña")]
        public string Contrasena { get; set; } = null!;

        public string? ReturnUrl { get; set; }
    }
}
