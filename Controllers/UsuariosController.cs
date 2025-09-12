using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Rotativa.AspNetCore;

using PIOGHOASIS.Helpers;
using PIOGHOASIS.Infraestructure.Data;
using PIOGHOASIS.Models.Entities;

namespace PIOGHOASIS.Controllers
{
    [Authorize]
    public class UsuariosController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public UsuariosController(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        private bool IsAjax => Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        private const string SqlCollation = "SQL_Latin1_General_CP1_CI_AI"; // SQL Server CI/AI

        // ========= Utilidades =========

        private async Task CargarCombosAsync(string? rolId = null, string? empleadoId = null)
        {
            // Roles
            var roles = await _db.roles
                .AsNoTracking()
                .Where(r => r.Estado)
                .OrderBy(r => r.Nombre)
                .Select(r => new SelectListItem { Value = r.RolID, Text = r.Nombre })
                .ToListAsync();
            ViewBag.Roles = new SelectList(roles, "Value", "Text", rolId);

            // Empleados activos SIN usuario; en edición, incluir el empleado actual
            var empleadosQ = _db.empleados
                .AsNoTracking()
                .Where(e => e.Estado)
                .Where(e =>
                    !_db.usuarios.Any(u => u.EmpleadoID == e.EmpleadoID)  // libres
                    || (empleadoId != null && e.EmpleadoID == empleadoId) // o el seleccionado (edición)
                );

            var empleadosRaw = await empleadosQ
                .OrderBy(e => e.EmpleadoID)
                .Select(e => new
                {
                    e.EmpleadoID,
                    e.Persona.PrimerNombre,
                    e.Persona.SegundoNombre,
                    e.Persona.PrimerApellido,
                    e.Persona.SegundoApellido
                })
                .ToListAsync();

            var empleados = empleadosRaw.Select(e => new SelectListItem
            {
                Value = e.EmpleadoID,
                Text = e.EmpleadoID + " - " + string.Join(" ",
                    new[] { e.PrimerNombre, e.SegundoNombre, e.PrimerApellido, e.SegundoApellido }
                        .Where(x => !string.IsNullOrWhiteSpace(x)))
            }).ToList();

            ViewBag.Empleados = new SelectList(empleados, "Value", "Text", empleadoId);
        }

        private async Task<string> NextUsuarioIdAsync()
        {
            var ids = await _db.usuarios
                .AsNoTracking()
                .Where(x => x.UsuarioID.StartsWith("USR"))
                .Select(x => x.UsuarioID)
                .ToListAsync();

            int max = 0;
            foreach (var id in ids)
            {
                // extrae la parte numérica después del prefijo
                var digits = new string(id.SkipWhile(c => !char.IsDigit(c)).ToArray());
                if (int.TryParse(digits, out var n) && n > max) max = n;
            }

            // siguiente disponible (con chequeo por si hubiera choque)
            string next;
            do
            {
                max++;
                next = $"USR{max:0000000}";
            }
            while (await _db.usuarios.AnyAsync(u => u.UsuarioID == next));

            return next;
        }

        private IQueryable<Usuario> BaseQuery() =>
            _db.usuarios
              .AsNoTracking()
              .Include(u => u.Rol)
              .Include(u => u.Empleado).ThenInclude(e => e.Persona);

        private IQueryable<Usuario> FiltrarUsuariosSqlServer(
            IQueryable<Usuario> q,
            string? codigo, string? nombre, string? rol, string? estado)
        {
            if (!string.IsNullOrWhiteSpace(codigo))
                q = q.Where(u => u.UsuarioID.Contains(codigo));

            if (!string.IsNullOrWhiteSpace(nombre))
            {
                var term = nombre.Trim();
                q = q.Where(u =>
                    EF.Functions.Like(EF.Functions.Collate(u.UsuarioNombre ?? "", SqlCollation), $"%{term}%") ||
                    EF.Functions.Like(
                        EF.Functions.Collate(
                            ((u.Empleado.Persona.PrimerNombre ?? "") + " " +
                             (u.Empleado.Persona.SegundoNombre ?? "") + " " +
                             (u.Empleado.Persona.PrimerApellido ?? "") + " " +
                             (u.Empleado.Persona.SegundoApellido ?? "")),
                            SqlCollation),
                        $"%{term}%")
                );
            }

            // Rol: acepta RolID exacto o coincidencia por NOMBRE (CI/AI)
            if (!string.IsNullOrWhiteSpace(rol))
            {
                var term = rol.Trim();
                q = q.Where(u =>
                    //EF.Functions.Like(EF.Functions.Collate(u.Rol.Nombre ?? "", SqlCollation), $"%{term}%") ||
                    EF.Functions.Like(EF.Functions.Collate(u.Rol.Nombre ?? "", SqlCollation), $"%{term}%")
                );
            }


            if (!string.IsNullOrWhiteSpace(estado))
            {
                if (estado == "1") q = q.Where(u => u.Estado);
                else if (estado == "0") q = q.Where(u => !u.Estado);
            }

            return q;
        }

        // ========= LISTADO =========
        [HttpGet]
        public async Task<IActionResult> Index(
            [FromQuery] string? codigo,
            [FromQuery] string? nombre,
            [FromQuery] string? rol,
            [FromQuery] string? estado)
        {
            if (!Request.Query.ContainsKey("estado")) estado = "1";

            await CargarCombosAsync();
            var q = FiltrarUsuariosSqlServer(BaseQuery(), codigo, nombre, rol, estado);

            var model = await q.OrderBy(u => u.UsuarioID).ToListAsync();
            ViewBag.IsPartial = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
            return View(model);
        }

        // ========= PDF =========
        [HttpGet]
        public async Task<IActionResult> ExportPdf(
            [FromQuery] string? codigo,
            [FromQuery] string? nombre,
            [FromQuery] string? rol,
            [FromQuery] string? estado)
        {
            var q = FiltrarUsuariosSqlServer(BaseQuery(), codigo, nombre, rol, estado);
            var model = await q.OrderBy(u => u.UsuarioID).ToListAsync();

            return new ViewAsPdf("ReportePdf", model)
            {
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,
                CustomSwitches = "--footer-center \"Página [page] de [toPage]\" --footer-font-size 8 --footer-spacing 5"
            };
        }

        // ========= CREATE =========
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await CargarCombosAsync();
            var nuevo = new Usuario
            {
                UsuarioID = await NextUsuarioIdAsync(),
                Estado = true,
                FechaRegistro = DateTime.Now
            };
            return IsAjax ? PartialView(nuevo) : View(nuevo);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Usuario model)
        {
            // Estas propiedades de navegación se validan fuera del modelo
            ModelState.Remove(nameof(Usuario.Rol));
            ModelState.Remove(nameof(Usuario.Empleado));

            var nombreTrim = (model.UsuarioNombre ?? "").Trim();
            if (await _db.usuarios.AnyAsync(u => u.UsuarioNombre == nombreTrim))
                ModelState.AddModelError(nameof(Usuario.UsuarioNombre), "Ya existe un usuario con ese nombre.");

            if (!ModelState.IsValid)
            {
                await CargarCombosAsync(model.RolID, model.EmpleadoID);
                return IsAjax ? PartialView(nameof(Create), model) : View(nameof(Create), model);
            }

            model.Contrasena = Pbkdf2.HashPassword(model.NuevaContrasena!);

            if (!string.IsNullOrWhiteSpace(model.EmpleadoID))
            {
                var ocupado = await _db.usuarios.AnyAsync(u => u.EmpleadoID == model.EmpleadoID);
                if (ocupado)
                    ModelState.AddModelError(nameof(Usuario.EmpleadoID), "El empleado ya tiene un usuario.");
            }


            try
            {
                model.UsuarioID = await NextUsuarioIdAsync();
                model.UsuarioNombre = nombreTrim;
                model.Contrasena = Pbkdf2.HashPassword(model.NuevaContrasena!); // <- byte[]
                model.FechaRegistro = DateTime.Now;

                _db.usuarios.Add(model);
                await _db.SaveChangesAsync();

                if (IsAjax) return Ok(new { ok = true, redirectUrl = Url.Action(nameof(Index)) });
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Error al guardar: " + ex.Message);
                await CargarCombosAsync(model.RolID, model.EmpleadoID);
                return IsAjax ? PartialView(nameof(Create), model) : View(nameof(Create), model);
            }

        }


        // ========= EDIT =========
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var u = await _db.usuarios
                .Include(x => x.Rol)
                .Include(x => x.Empleado).ThenInclude(e => e.Persona)
                .FirstOrDefaultAsync(x => x.UsuarioID == id);

            if (u == null) return NotFound();

            await CargarCombosAsync(u.RolID, u.EmpleadoID);
            return IsAjax ? PartialView(u) : View(u);
        }

        //[HttpPost, ValidateAntiForgeryToken]
        //public async Task<IActionResult> Edit(Usuario form)
        //{
        //    ModelState.Remove(nameof(Usuario.Rol));
        //    ModelState.Remove(nameof(Usuario.Empleado));

        //    var db = await _db.usuarios.FirstOrDefaultAsync(x => x.UsuarioID == form.UsuarioID);
        //    if (db == null) return NotFound();

        //    var nombreTrim = (form.UsuarioNombre ?? "").Trim();
        //    if (await _db.usuarios.AnyAsync(u => u.UsuarioNombre == nombreTrim && u.UsuarioID != form.UsuarioID))
        //        ModelState.AddModelError(nameof(Usuario.UsuarioNombre), "Ya existe un usuario con ese nombre.");

        //    // Si NO quieren cambiar contraseña, omite validaciones de esos campos
        //    var pwVacia = string.IsNullOrWhiteSpace(form.NuevaContrasena) && string.IsNullOrWhiteSpace(form.ConfirmarContrasena);
        //    if (pwVacia)
        //    {
        //        ModelState.Remove(nameof(Usuario.NuevaContrasena));
        //        ModelState.Remove(nameof(Usuario.ConfirmarContrasena));
        //    }
        //    else
        //    {
        //        // Validar sólo si escribieron algo
        //        if (string.IsNullOrWhiteSpace(form.NuevaContrasena))
        //            ModelState.AddModelError(nameof(Usuario.NuevaContrasena), "Ingresa la nueva contraseña.");
        //        else if (form.NuevaContrasena != form.ConfirmarContrasena)
        //            ModelState.AddModelError(nameof(Usuario.ConfirmarContrasena), "La confirmación no coincide.");
        //        // (Opcional) aquí puedes verificar reglas de complejidad si las necesitas
        //    }

        //    // Valida el empleado único también (si cambian EmpleadoID)
        //    if (!string.IsNullOrWhiteSpace(form.EmpleadoID))
        //    {
        //        var ocupado = await _db.usuarios
        //            .AnyAsync(u => u.EmpleadoID == form.EmpleadoID && u.UsuarioID != form.UsuarioID);
        //        if (ocupado)
        //            ModelState.AddModelError(nameof(Usuario.EmpleadoID), "El empleado ya tiene un usuario.");
        //    }

        //    if (!ModelState.IsValid)
        //    {
        //        await CargarCombosAsync(form.RolID, form.EmpleadoID);
        //        return IsAjax ? PartialView(nameof(Edit), form) : View(nameof(Edit), form);
        //    }

        //    // Actualiza campos normales
        //    db.UsuarioNombre = nombreTrim;
        //    db.RolID = form.RolID;
        //    db.EmpleadoID = form.EmpleadoID;
        //    db.Estado = form.Estado;

        //    // Sólo cambia contraseña si se escribió una nueva
        //    if (!pwVacia)
        //        db.Contrasena = Pbkdf2.HashPassword(form.NuevaContrasena!);

        //    await _db.SaveChangesAsync();

        //    if (IsAjax) return Ok(new { ok = true, redirectUrl = Url.Action(nameof(Index)) });
        //    return RedirectToAction(nameof(Index));
        //}

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Usuario form)
        {
            ModelState.Remove(nameof(Usuario.Rol));
            ModelState.Remove(nameof(Usuario.Empleado));

            var db = await _db.usuarios.FirstOrDefaultAsync(x => x.UsuarioID == form.UsuarioID);
            if (db == null) return NotFound();

            var nombreTrim = (form.UsuarioNombre ?? "").Trim();
            if (await _db.usuarios.AnyAsync(u => u.UsuarioNombre == nombreTrim && u.UsuarioID != form.UsuarioID))
                ModelState.AddModelError(nameof(Usuario.UsuarioNombre), "Ya existe un usuario con ese nombre.");

            // Contraseña opcional
            var pwVacia = string.IsNullOrWhiteSpace(form.NuevaContrasena) &&
                          string.IsNullOrWhiteSpace(form.ConfirmarContrasena);
            if (pwVacia)
            {
                ModelState.Remove(nameof(Usuario.NuevaContrasena));
                ModelState.Remove(nameof(Usuario.ConfirmarContrasena));
            }
            else
            {
                if (string.IsNullOrWhiteSpace(form.NuevaContrasena))
                    ModelState.AddModelError(nameof(Usuario.NuevaContrasena), "Ingresa la nueva contraseña.");
                else if (form.NuevaContrasena != form.ConfirmarContrasena)
                    ModelState.AddModelError(nameof(Usuario.ConfirmarContrasena), "La confirmación no coincide.");
            }

            // Empleado único
            if (!string.IsNullOrWhiteSpace(form.EmpleadoID))
            {
                var ocupado = await _db.usuarios.AnyAsync(u => u.EmpleadoID == form.EmpleadoID && u.UsuarioID != form.UsuarioID);
                if (ocupado)
                    ModelState.AddModelError(nameof(Usuario.EmpleadoID), "El empleado ya tiene un usuario.");
            }

            if (!ModelState.IsValid)
            {
                await CargarCombosAsync(form.RolID, form.EmpleadoID);
                return IsAjax ? PartialView(nameof(Edit), form) : View(nameof(Edit), form);
            }

            // ===== Detección de cambios =====
            var hadChanges =
                db.UsuarioNombre != nombreTrim ||
                db.RolID != form.RolID ||
                db.EmpleadoID != form.EmpleadoID ||
                db.Estado != form.Estado ||
                !pwVacia; // si escribió una nueva contraseña, cuenta como cambio

            if (!hadChanges)
            {
                if (IsAjax)
                    return Ok(new { ok = false, reason = "nochanges", message = "No has modificado ningún campo." });

                // Fallback no-AJAX
                ModelState.AddModelError(string.Empty, "No has modificado ningún campo.");
                await CargarCombosAsync(form.RolID, form.EmpleadoID);
                return View(nameof(Edit), form);
            }

            // ===== Aplicar cambios y guardar =====
            db.UsuarioNombre = nombreTrim;
            db.RolID = form.RolID;
            db.EmpleadoID = form.EmpleadoID;
            db.Estado = form.Estado;

            if (!pwVacia)
                db.Contrasena = Pbkdf2.HashPassword(form.NuevaContrasena!);

            await _db.SaveChangesAsync();

            if (IsAjax) return Ok(new { ok = true, redirectUrl = Url.Action(nameof(Index)) });
            return RedirectToAction(nameof(Index));
        }


        // ========= DETAILS =========
        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            var u = await _db.usuarios
                .AsNoTracking()
                .Include(x => x.Rol)
                .Include(x => x.Empleado).ThenInclude(e => e.Persona)
                .FirstOrDefaultAsync(x => x.UsuarioID == id);

            if (u == null) return NotFound();
            return IsAjax ? PartialView(u) : View(u);
        }

        // ========= DELETE =========
        [HttpGet]
        public async Task<IActionResult> Delete(string id)
        {
            var u = await _db.usuarios
                .AsNoTracking()
                .Include(x => x.Rol)
                .Include(x => x.Empleado).ThenInclude(e => e.Persona)
                .FirstOrDefaultAsync(x => x.UsuarioID == id);

            if (u == null) return NotFound();
            return IsAjax ? PartialView(u) : View(u);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleEstado(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();
            var u = await _db.usuarios.FirstOrDefaultAsync(x => x.UsuarioID == id);
            if (u == null) return NotFound();

            u.Estado = !u.Estado;
            await _db.SaveChangesAsync();

            if (IsAjax)
                return Ok(new { ok = true, message = u.Estado ? "Usuario reactivado" : "Usuario desactivado", redirectUrl = Url.Action(nameof(Index)) });

            return RedirectToAction(nameof(Index));
        }

        // ========= AVATAR =========
        // GET /Usuarios/Avatar/USU0000001
        [HttpGet("/Usuarios/Avatar/{id}")]
        [ResponseCache(Duration = 1200, Location = ResponseCacheLocation.Client, NoStore = false)]
        public async Task<IActionResult> Avatar(string id)
        {
            // 1) Tomar solo el FotoPath (puede ser null)
            var fotoRel = await _db.usuarios
                .AsNoTracking()
                .Where(u => u.UsuarioID == id)
                .Select(u => u.Empleado.Persona.FotoPath)
                .FirstOrDefaultAsync();

            // 2) Paths base
            var defaultPath = Path.Combine(_env.WebRootPath, "img", "DefaultUsuario.png");

            // 3) Si no hay ruta, devuelve default
            if (string.IsNullOrWhiteSpace(fotoRel))
                return PhysicalFile(defaultPath, "image/png");

            // 4) Normalizar/asegurar que sea relativa a wwwroot
            var rel = fotoRel.Replace('\\', '/').TrimStart('~').TrimStart('/');
            var full = Path.Combine(_env.WebRootPath, rel);

            // 5) Si no existe el archivo, default
            if (!System.IO.File.Exists(full))
                return PhysicalFile(defaultPath, "image/png");

            // 6) Content-Type a partir de la extensión
            var contentType = Path.GetExtension(full).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };

            return PhysicalFile(full, contentType);
        }
    }
}
