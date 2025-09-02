using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using PIOGHOASIS.Helpers;
using PIOGHOASIS.Infraestructure.Data;
using PIOGHOASIS.Models;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

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

            // claims (agrega lo que necesites)
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UsuarioID),
                new Claim(ClaimTypes.Name, displayName),
                new Claim(ClaimTypes.Role, roleName),       // ← guardamos NOMBRE del rol
                new Claim("role_id", roleId),               // ← opcional: ID del rol
                new Claim("avatar", avatarUrl)
            };

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

        //[HttpGet]                 // GET /Login  y  GET /Login/Index
        //public IActionResult Index()
        //{
        //    return View();        // busca Views/Login/Index.cshtml
        //}

        //[HttpPost]
        //public IActionResult Index(string Usuario, string Contrasena)
        //{
        //    // lógica de autenticación (luego la agregamos)
        //    return View();
        //}
    }
}
