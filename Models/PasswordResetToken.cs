using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace PIOGHOASIS.Models
{
    [Table("PASSWORD_RESET_TOKENS", Schema = "dbo")]
    public class PasswordResetToken
    {
        [Key]
        public Guid Id { get; set; }

        [Required, StringLength(20)]
        public string UsuarioID { get; set; } = null!;   // FK al usuario

        // Guardamos SOLO el hash del token (Base64Url del SHA256)
        [Required, StringLength(64)]
        public string TokenHash { get; set; } = null!;

        public DateTime ExpiresAtUtc { get; set; }       // UtcNow + 5 min
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? UsedAtUtc { get; set; }      // null = no usado

        [StringLength(64)] public string? RequestIp { get; set; }
        [StringLength(256)] public string? UserAgent { get; set; }
    }
}
