using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.WebUtilities;
using PIOGHOASIS.Infraestructure.Data;
using PIOGHOASIS.Models.Entities;
using PIOGHOASIS.Helpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using PIOGHOASIS.Models; // Pbkdf2
// VMs
public record ForgotVm(string Email);
public class ResetVm
{
    public string? Uid { get; set; }
    public string? Token { get; set; }
    public string? NuevaContrasena { get; set; }
    public string? ConfirmarContrasena { get; set; }
}

public class PasswordController : Controller
{
    private readonly AppDbContext _db;
    private readonly IEmailSender _email;

    public PasswordController(AppDbContext db, IEmailSender email)
    {
        _db = db; _email = email;
    }

    // =========== 1) Solicitud ===========
    [HttpGet]
    public IActionResult Forgot() => View(new ForgotVm(""));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Forgot(ForgotVm vm)
    {
        // 1) Ubicar usuario por correo de Persona (ajusta el nombre del campo!)
        var email = (vm.Email ?? "").Trim();
        var user = await _db.usuarios
            .Include(u => u.Empleado).ThenInclude(e => e.Persona)
            .FirstOrDefaultAsync(u => u.Estado &&
                (u.Empleado.Persona.Email ?? "") == email); // <-- ajusta

        // 2) Si existe, generar token (pero la respuesta al cliente será siempre la misma)
        if (user != null)
        {
            // invalidar tokens previos vigentes del mismo usuario
            var actives = await _db.password_reset_tokens
                .Where(t => t.UsuarioID == user.UsuarioID && t.UsedAtUtc == null && t.ExpiresAtUtc > DateTime.UtcNow)
                .ToListAsync();
            _db.password_reset_tokens.RemoveRange(actives);

            // token aleatorio URL-safe
            var raw = RandomNumberGenerator.GetBytes(32);
            var token = WebEncoders.Base64UrlEncode(raw);

            // hash SHA256 (Base64Url)
            using var sha = SHA256.Create();
            var hash = WebEncoders.Base64UrlEncode(sha.ComputeHash(Encoding.UTF8.GetBytes(token)));

            var rec = new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UsuarioID = user.UsuarioID,
                TokenHash = hash,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
                RequestIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = Request.Headers.UserAgent.ToString()
            };
            _db.password_reset_tokens.Add(rec);
            await _db.SaveChangesAsync();

            // link absoluto
            var link = Url.Action("Reset", "Password",
                new { uid = user.UsuarioID, token },
                protocol: Request.Scheme)!;

            var html = $@"
<table style=""font-family:Arial,sans-serif;font-size:15px;line-height:1.5;color:#222;width:100%;"">
<tr><td>
  <h2>Restablecer contraseña</h2>
  <p>Has solicitado restablecer tu contraseña de <strong>Hotel Oasis</strong>.</p>
  <p>Haz clic en el botón dentro de los próximos <strong>5 minutos</strong>:</p>
  <p style=""margin:22px 0"">
    <a href=""{link}"" style=""background:#f0a100;color:#222;padding:12px 18px;border-radius:8px;
                          text-decoration:none;display:inline-block;font-weight:bold;"">Cambiar contraseña</a>
  </p>
  <p>Si no solicitaste este cambio, puedes ignorar este mensaje.</p>
</td></tr></table>";

            await _email.SendEmailAsync(email, "Restablecer contraseña - Hotel Oasis", html);
        }

        // Respuesta genérica
        TempData["ForgotOk"] = "Si el correo corresponde a una cuenta, hemos enviado instrucciones para restablecer la contraseña.";
        return RedirectToAction(nameof(Forgot));
    }

    // =========== 2) Formulario de cambio ===========
    [HttpGet]
    public async Task<IActionResult> Reset(string uid, string token)
    {
        if (string.IsNullOrWhiteSpace(uid) || string.IsNullOrWhiteSpace(token))
            return View("ResetInvalid");

        var rec = await FindValidRecord(uid, token);
        if (rec == null) return View("ResetInvalid");

        return View(new ResetVm { Uid = uid, Token = token });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Reset(ResetVm vm)
    {
        if (string.IsNullOrWhiteSpace(vm.Uid) || string.IsNullOrWhiteSpace(vm.Token))
            return View("ResetInvalid");

        // Validaciones mínimas (mismas reglas que Create)
        var p = vm.NuevaContrasena ?? "";
        if (!(p.Any(char.IsLower) && p.Any(char.IsUpper) && p.Any(char.IsDigit) && p.Length >= 8 && p.Length <= 15))
            ModelState.AddModelError(nameof(vm.NuevaContrasena), "La contraseña no cumple los requisitos.");
        if (vm.NuevaContrasena != vm.ConfirmarContrasena)
            ModelState.AddModelError(nameof(vm.ConfirmarContrasena), "La confirmación no coincide.");

        if (!ModelState.IsValid)
            return View(vm);

        var rec = await FindValidRecord(vm.Uid!, vm.Token!);
        if (rec == null) return View("ResetInvalid");

        var user = await _db.usuarios.FirstOrDefaultAsync(u => u.UsuarioID == vm.Uid);
        if (user == null) return View("ResetInvalid");

        // Cambiar contraseña
        user.Contrasena = Pbkdf2.HashPassword(vm.NuevaContrasena!);
        rec.UsedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return View("ResetSuccess");
    }

    // ===== Helper: valida token/expiración/uso =====
    private async Task<PasswordResetToken?> FindValidRecord(string uid, string token)
    {
        using var sha = SHA256.Create();
        var hash = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(
            sha.ComputeHash(Encoding.UTF8.GetBytes(token)));

        var rec = await _db.password_reset_tokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.UsuarioID == uid && t.TokenHash == hash);

        if (rec == null) return null;
        if (rec.UsedAtUtc != null) return null;
        if (rec.ExpiresAtUtc <= DateTime.UtcNow) return null;

        return rec;
    }
}
