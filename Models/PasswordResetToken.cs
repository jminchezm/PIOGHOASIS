using PIOGHOASIS.Models.Entities;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("PASSWORD_RESET_TOKENS", Schema = "dbo")]
public class PasswordResetToken
{
    [Key] public Guid Id { get; set; }

    [Required, Column("UsuarioID"), MaxLength(10)]
    public string UsuarioID { get; set; } = null!;

    [Required, MaxLength(64)]
    public string TokenHash { get; set; } = null!;

    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    [MaxLength(64)] public string? RequestIp { get; set; }
    [MaxLength(256)] public string? UserAgent { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // La navegación; la FK ya queda definida por Fluent API
    public Usuario Usuario { get; set; } = null!;
}
