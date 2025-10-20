using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PIOGHOASIS.Infraestructure.Data;
using PIOGHOASIS.Infraestructure.Security;
using PIOGHOASIS.Models;
using System.Linq;
using System.Threading.Tasks;

namespace PIOGHOASIS.Controllers
{
    //[RequireModule("MODULOS")]
    public class ModulosController : Controller
    {
        private readonly AppDbContext _context;
        public ModulosController(AppDbContext context) => _context = context;

        private bool IsAjax => Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        // ===== INDEX con filtros (por defecto Estado=Activo) =====
        public async Task<IActionResult> Index(string? codigo, string? nombre, string? estado)
        {
            if (!Request.Query.ContainsKey("estado"))
                estado = "1"; // Activo por defecto

            var q = _context.modulos.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(codigo))
                q = q.Where(m => m.Codigo.Contains(codigo));

            if (!string.IsNullOrWhiteSpace(nombre))
                q = q.Where(m => m.Nombre.Contains(nombre));

            if (!string.IsNullOrWhiteSpace(estado))
            {
                if (estado == "1") q = q.Where(m => m.Estado);
                else if (estado == "0") q = q.Where(m => !m.Estado);
            }

            var model = await q.OrderBy(m => m.Codigo).ToListAsync();
            return IsAjax ? PartialView(nameof(Index), model) : View(model);
        }

        // ===== DETAILS =====
        public async Task<IActionResult> Details(int id)
        {
            var modulo = await _context.modulos.AsNoTracking().FirstOrDefaultAsync(m => m.ModuloID == id);
            if (modulo == null) return NotFound();
            return IsAjax ? PartialView(modulo) : View(modulo);
        }

        private async Task<string> NextCodigoAsync()
        {
            // Trae todos los códigos que empiezan con MOD (por si algún día hay otros)
            var codigos = await _context.modulos
                .AsNoTracking()
                .Where(m => m.Codigo.StartsWith("MOD"))
                .Select(m => m.Codigo)
                .ToListAsync();

            // Saca la parte numérica y calcula el mayor
            int max = 0;
            foreach (var c in codigos)
            {
                var digits = new string(c.Where(char.IsDigit).ToArray());
                if (int.TryParse(digits, out var n) && n > max) max = n;
            }

            // Siguiente, con 7 dígitos (MOD + 0000001)
            return $"MOD{(max + 1).ToString("D7")}";
        }

        // ===== CREATE =====
        public async Task<IActionResult> Create()
        {
            var model = new Modulo
            {
                Codigo = await NextCodigoAsync(),   // ← autogenerado
                Estado = true,
                FechaRegistro = DateTime.Now
            };
            return IsAjax ? PartialView(model) : View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Codigo,Nombre,Descripcion,Estado,FechaRegistro")] Modulo modulo)
        {
            // Normaliza antes de validar/guardar
            modulo.Nombre = N(modulo.Nombre);
            modulo.Descripcion = N(modulo.Descripcion);
            modulo.Estado = true;

            if (!ModelState.IsValid)
                return IsAjax ? PartialView(modulo) : View(modulo);

            // Nombre único (APP)
            var nombreTomado = await _context.modulos
                .AnyAsync(m => m.Nombre == modulo.Nombre); // si tu collation es CI, esto ya es insensible a may/min
            if (nombreTomado)
            {
                ModelState.AddModelError(nameof(modulo.Nombre), "Ya existe un módulo con ese nombre.");
                return IsAjax ? PartialView(modulo) : View(modulo);
            }

            // Si no trae código válido, genera uno con prefijo MOD
            if (string.IsNullOrWhiteSpace(modulo.Codigo) || !modulo.Codigo.StartsWith("MOD"))
                modulo.Codigo = await NextCodigoAsync();

            // Por si alguien guardó justo antes (colar un nuevo correlativo)
            while (await _context.modulos.AnyAsync(x => x.Codigo == modulo.Codigo))
                modulo.Codigo = await NextCodigoAsync();

            // Fecha de registro controlada en servidor
            modulo.FechaRegistro = DateTime.Now;

            _context.Add(modulo);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex) when (IsUniqueViolation(ex))
            {
                // respaldo si hay índice único a nivel BD
                ModelState.AddModelError(nameof(modulo.Nombre), "Ya existe un módulo con ese nombre.");
                return IsAjax ? PartialView(modulo) : View(modulo);
            }

            if (IsAjax) return Json(new { ok = true, redirectUrl = Url.Action(nameof(Index)) });
            return RedirectToAction(nameof(Index));
        }



        // ===== EDIT =====
        public async Task<IActionResult> Edit(int id)
        {
            var modulo = await _context.modulos.AsNoTracking().FirstOrDefaultAsync(m => m.ModuloID == id);
            if (modulo == null) return NotFound();
            return IsAjax ? PartialView(modulo) : View(modulo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ModuloID,Codigo,Nombre,Descripcion,Estado,FechaRegistro")] Modulo modulo)
        {
            if (id != modulo.ModuloID) return NotFound();

            // Normaliza entrada
            var nuevoNombre = N(modulo.Nombre);
            var nuevaDesc = N(modulo.Descripcion);

            if (!ModelState.IsValid)
                return IsAjax ? PartialView(modulo) : View(modulo);

            // Trae el registro actual de BD para comparar
            var db = await _context.modulos.FirstOrDefaultAsync(m => m.ModuloID == id);
            if (db == null) return NotFound();

            // Nombre único (excluyendo el propio registro)
            var nombreTomado = await _context.modulos
                .AnyAsync(m => m.ModuloID != id && m.Nombre == nuevoNombre);
            if (nombreTomado)
            {
                ModelState.AddModelError(nameof(modulo.Nombre), "Ya existe un módulo con ese nombre.");
                return IsAjax ? PartialView(modulo) : View(modulo);
            }

            // Detecta cambios con valores normalizados
            bool hadChanges =
                !string.Equals(N(db.Codigo), N(modulo.Codigo), StringComparison.Ordinal) ||
                !string.Equals(N(db.Nombre), nuevoNombre, StringComparison.Ordinal) ||
                !string.Equals(N(db.Descripcion), nuevaDesc, StringComparison.Ordinal) ||
                db.Estado != modulo.Estado
                // Si en la UI la fecha es solo lectura, normalmente no debería cambiar:
                || db.FechaRegistro != modulo.FechaRegistro;

            // Aplica cambios
            db.Nombre = nuevoNombre;
            db.Descripcion = nuevaDesc;
            db.Estado = modulo.Estado;
            db.Codigo = N(modulo.Codigo);        // si no se edita, puedes omitir
            db.FechaRegistro = modulo.FechaRegistro;   // si es RO, también puedes omitir esta línea

            if (!hadChanges)
            {
                if (IsAjax)
                    return Ok(new { ok = false, reason = "nochanges", message = "Realiza un cambio antes de guardar." });

                TempData["NoChanges"] = true;
                return View(modulo);
            }

            try
            {
                await _context.SaveChangesAsync();

                if (IsAjax) return Json(new { ok = true, redirectUrl = Url.Action(nameof(Index)) });
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (IsUniqueViolation(ex))
            {
                ModelState.AddModelError(nameof(modulo.Nombre), "Ya existe un módulo con ese nombre.");
                return IsAjax ? PartialView(modulo) : View(modulo);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Error al actualizar: " + ex.Message);
                return IsAjax ? PartialView(modulo) : View(modulo);
            }
        }


        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Edit(int id, [Bind("ModuloID,Codigo,Nombre,Descripcion,Estado,FechaRegistro")] Modulo modulo)
        //{
        //    if (id != modulo.ModuloID) return NotFound();
        //    if (!ModelState.IsValid)
        //        return IsAjax ? PartialView(modulo) : View(modulo);

        //    _context.Update(modulo);
        //    await _context.SaveChangesAsync();

        //    if (IsAjax) return Json(new { ok = true, redirectUrl = Url.Action(nameof(Index)) });
        //    return RedirectToAction(nameof(Index));
        //}

        // ===== DELETE (pantalla de confirmación) =====
        public async Task<IActionResult> Delete(int id)
        {
            var modulo = await _context.modulos.AsNoTracking().FirstOrDefaultAsync(m => m.ModuloID == id);
            if (modulo == null) return NotFound();
            return IsAjax ? PartialView(modulo) : View(modulo);
        }

        // ===== ToggleEstado (activar/desactivar) =====
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleEstado(int id)
        {
            var modulo = await _context.modulos.FindAsync(id);
            if (modulo == null) return NotFound();

            // cambia el estado
            modulo.Estado = !modulo.Estado;
            _context.Update(modulo);
            await _context.SaveChangesAsync();

            // Mensaje amigable según el NUEVO estado
            var msg = modulo.Estado ? "¡Módulo activado exitosamente!" : "¡Módulo desactivado exitosamente!";

            if (IsAjax)
            {
                // Igual que Create/Edit: JSON => el Dashboard mostrará el modal
                return Json(new
                {
                    ok = true,
                    message = msg,
                    redirectUrl = Url.Action(nameof(Index))
                });
            }

            // Navegación tradicional (sin modal temporizado)
            return RedirectToAction(nameof(Index));
        }

        private bool ModuloExists(int id) => _context.modulos.Any(e => e.ModuloID == id);

        // Helper de normalización (trim; si quieres fuerza CI usa ToUpper())
        private static string N(string? s) => (s ?? "").Trim();

        // (Opcional) Helper para atrapar violaciones de UNIQUE/PK de SQL Server
        private static bool IsUniqueViolation(Exception ex) =>
            ex is DbUpdateException dbu &&
            dbu.InnerException is SqlException sql &&
            (sql.Number == 2627 || sql.Number == 2601);

    }
}