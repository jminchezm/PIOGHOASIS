using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PIOGHOASIS.Helpers;
using PIOGHOASIS.Infraestructure.Data;
using PIOGHOASIS.Models;
using System.Security.Claims;

namespace PIOGHOASIS.Controllers
{
    public class LoginController : Controller
    {

        private readonly AppDbContext _db;

        public LoginController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public IActionResult Index(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(LoginViewModel vm, string? returnUrl = null)
        {
            if (!ModelState.IsValid)
            {
                TempData["LoginMessage"] = "Ingrese usuario y contraseña.";
                return View(vm);
            }

            //// busca usuario activo por nombre
            //var user = await _db.usuarios
            //    .AsNoTracking()
            //    .FirstOrDefaultAsync(u => u.UsuarioNombre == vm.Usuario && u.Estado == true);

            // busca usuario activo por nombre e incluye empleado+persona
            var user = await _db.usuarios
                .AsNoTracking()
                .Include(u => u.Rol)
                .Include(u => u.Empleado).ThenInclude(e => e.Persona)
                .FirstOrDefaultAsync(u => u.UsuarioNombre == vm.Usuario && u.Estado == true);

            if (user is null)
            {
                TempData["LoginMessage"] = "Usuario o contraseña incorrectos.";
                return View(vm);
            }

            // verifica PBKDF2 (Salt+Hash)
            bool ok = Pbkdf2.Verify(vm.Contrasena, user.Contrasena);
            if (!ok)
            {
                TempData["LoginMessage"] = "Usuario o contraseña incorrectos.";
                return View(vm);
            }

            string displayName = user.Empleado?.Persona is { } p
                ? $"{p.PrimerNombre} {p.PrimerApellido}".Trim()
                : user.UsuarioNombre;

            // nombre del rol para UI y Authorize
            string roleName = (user.Rol?.Estado ?? false) ? user.Rol!.Nombre : "Usuario";

            // opcional: deja también el ID del rol por si lo quieres usar en lógica
            string roleId = user.RolID ?? "N/A";

            string avatarUrl = Url.Action("Avatar", "Usuario", new { id = user.UsuarioID })
                                ?? Url.Content("~/img/DefaultUsuario.png");

            // === Cargar módulos permitidos del rol ===
            var modCodes = await _db.rolModuloPermisos
                .Where(p => p.RolID == roleId && p.PuedeAcceder)
                .Join(_db.modulos, p => p.ModuloID, m => m.ModuloID, (p, m) => m.Codigo)
                .Where(c => c != null && c != "")
                .Distinct()
                .ToListAsync();

            // Claim type para módulos (personalizado)
            const string ModuleClaimType = "perm.module";

            // claims (agrega lo que necesites)
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UsuarioID),
                new Claim(ClaimTypes.Name, displayName),
                new Claim(ClaimTypes.Role, roleName), // nombre del rol
                new Claim("role_id", roleId),         // id del rol (útil para refrescar)
                new Claim("avatar", avatarUrl)
            };

            // Agrega un claim por cada código de módulo permitido
            claims.AddRange(modCodes.Select(c => new Claim(ModuleClaimType, c)));

            //// claims (agrega lo que necesites)
            //var claims = new List<Claim>
            //{
            //    new Claim(ClaimTypes.NameIdentifier, user.UsuarioID),
            //    new Claim(ClaimTypes.Name, displayName),
            //    new Claim(ClaimTypes.Role, roleName),       // ← guardamos NOMBRE del rol
            //    new Claim("role_id", roleId),               // ← opcional: ID del rol
            //    new Claim("avatar", avatarUrl)
            //};



            var ci = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var cp = new ClaimsPrincipal(ci);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                cp,
                new AuthenticationProperties
                {
                    IsPersistent = true, // "Recordarme" si luego agregas checkbox
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                });

            // redirige a ReturnUrl valida o al Dashboard
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Dashboard", "Home"); // tu dashboard
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Salir()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Login");
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RefrescarPermisos()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Forbid();

            var user = await _db.usuarios
                .AsNoTracking()
                .Include(u => u.Rol)
                .Include(u => u.Empleado).ThenInclude(e => e.Persona)
                .FirstOrDefaultAsync(u => u.UsuarioID == userId && u.Estado);

            if (user is null) return Forbid();

            string displayName = user.Empleado?.Persona is { } p
                ? $"{p.PrimerNombre} {p.PrimerApellido}".Trim()
                : user.UsuarioNombre;

            string roleName = (user.Rol?.Estado ?? false) ? user.Rol!.Nombre : "Usuario";
            string roleId = user.RolID ?? "N/A";

            var avatarUrl = Url.Action("Avatar", "Usuarios", new { id = user.UsuarioID })
                            ?? Url.Content("~/img/DefaultUsuario.png");

            // Recalcular módulos
            var modCodes = await _db.rolModuloPermisos
                .Where(p => p.RolID == roleId && p.PuedeAcceder)
                .Join(_db.modulos, p => p.ModuloID, m => m.ModuloID, (p, m) => m.Codigo)
                .Where(c => c != null && c != "")
                .Distinct()
                .ToListAsync();

            const string ModuleClaimType = "perm.module";

            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, user.UsuarioID),
        new Claim(ClaimTypes.Name, displayName),
        new Claim(ClaimTypes.Role, roleName),
        new Claim("role_id", roleId),
        new Claim("avatar", avatarUrl)
    };
            claims.AddRange(modCodes.Select(c => new Claim(ModuleClaimType, c)));

            var ci = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var cp = new ClaimsPrincipal(ci);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                cp,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                });

            // Vuelve al dashboard (o JSON si lo llamas por AJAX)
            return RedirectToAction("Dashboard", "Home");
        }
    }
}
