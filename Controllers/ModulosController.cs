using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PIOGHOASIS.Infraestructure.Data;
using PIOGHOASIS.Models;
using PIOGHOASIS.Infraestructure.Security;

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
            if (!ModelState.IsValid)
                return IsAjax ? PartialView(modulo) : View(modulo);

            // Si no trae código válido, o no comienza con “MOD”, genera uno
            if (string.IsNullOrWhiteSpace(modulo.Codigo) || !modulo.Codigo.StartsWith("MOD"))
                modulo.Codigo = await NextCodigoAsync();

            // Por si alguien guardó justo antes (colar un nuevo correlativo)
            while (await _context.modulos.AnyAsync(x => x.Codigo == modulo.Codigo))
                modulo.Codigo = await NextCodigoAsync();

            // Fecha de registro controlada en servidor
            modulo.FechaRegistro = DateTime.Now;

            _context.Add(modulo);
            await _context.SaveChangesAsync();

            if (IsAjax) return Json(new { ok = true, redirectUrl = Url.Action(nameof(Index)) });
            return RedirectToAction(nameof(Index));
        }


        // ===== EDIT =====
        public async Task<IActionResult> Edit(int id)
        {
            var modulo = await _context.modulos.FindAsync(id);
            if (modulo == null) return NotFound();
            return IsAjax ? PartialView(modulo) : View(modulo);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ModuloID,Codigo,Nombre,Descripcion,Estado,FechaRegistro")] Modulo modulo)
        {
            if (id != modulo.ModuloID) return NotFound();

            if (!ModelState.IsValid)
                return IsAjax ? PartialView(modulo) : View(modulo);

            // Trae el registro actual de BD para comparar
            var db = await _context.modulos.AsNoTracking().FirstOrDefaultAsync(m => m.ModuloID == id);
            if (db == null) return NotFound();

            // Normaliza para comparar (evita falsos positivos por espacios o nulls)
            string N(string? s) => (s ?? "").Trim();

            bool hadChanges =
                !string.Equals(N(db.Codigo), N(modulo.Codigo), StringComparison.Ordinal) ||
                !string.Equals(N(db.Nombre), N(modulo.Nombre), StringComparison.Ordinal) ||
                !string.Equals(N(db.Descripcion), N(modulo.Descripcion), StringComparison.Ordinal) ||
                db.Estado != modulo.Estado
                // FechaRegistro en tu UI es solo lectura; si cambia, también cuenta:
                || db.FechaRegistro != modulo.FechaRegistro;

            if (!hadChanges)
            {
                if (IsAjax)
                    return Ok(new { ok = false, reason = "nochanges", message = "Realiza un cambio antes de guardar." });

                // Flujo no-AJAX: vuelve a la vista con un aviso sencillo (opcional)
                TempData["NoChanges"] = true;
                return View(modulo);
            }

            // Sí hubo cambios -> guarda
            try
            {
                _context.Update(modulo);
                await _context.SaveChangesAsync();

                if (IsAjax) return Json(new { ok = true, redirectUrl = Url.Action(nameof(Index)) });
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                // Manejo simple de error
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
    }
}