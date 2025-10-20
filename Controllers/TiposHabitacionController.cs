using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PIOGHOASIS.Infraestructure.Data;
using PIOGHOASIS.Models;
using Rotativa.AspNetCore;
using Rotativa.AspNetCore.Options;
using System.Linq;
using System.Threading.Tasks;

namespace PIOGHOASIS.Controllers
{
    // Soporta TAMBIÉN /Habitaciones/Tipos/... por compatibilidad
    [Route("TiposHabitacion")]
    //[Route("Habitaciones/Tipos")]
    public class TiposHabitacionController : Controller
    {
        private readonly AppDbContext _db;
        public TiposHabitacionController(AppDbContext db) => _db = db;

        private bool IsAjax => Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        // ====== INDEX (con filtros y Activo por defecto) ======
        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index(string? codigo, string? nombre, string? estado)
        {
            if (!Request.Query.ContainsKey("estado")) estado = "1";

            var q = _db.tiposHabitacion.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(codigo))
                q = q.Where(t => t.TipoHabitacionID.Contains(codigo));

            if (!string.IsNullOrWhiteSpace(nombre))
                q = q.Where(t => t.Nombre.Contains(nombre));

            if (!string.IsNullOrWhiteSpace(estado))
            {
                if (estado == "1") q = q.Where(t => t.Estado);
                else if (estado == "0") q = q.Where(t => !t.Estado);
            }

            var model = await q.OrderBy(t => t.TipoHabitacionID).ToListAsync();
            return IsAjax ? PartialView(nameof(Index), model) : View(model);
        }

        private async Task<string> NextTipoHabIdAsync()
        {
            var ids = await _db.tiposHabitacion
                .AsNoTracking()
                .Where(x => x.TipoHabitacionID.StartsWith("TIPHAB"))  // <- aquí
                .Select(x => x.TipoHabitacionID)
                .ToListAsync();

            int max = 0;
            foreach (var id in ids)
            {
                var digits = new string(id.SkipWhile(c => !char.IsDigit(c)).ToArray());
                if (int.TryParse(digits, out var n) && n > max) max = n;
            }
            return $"TIPHAB{(max + 1):D4}";
        }


        // ====== CREATE ======
        [HttpGet("Create")]
        public async Task<IActionResult> Create()
        {
            var model = new TipoHabitacion
            {
                TipoHabitacionID = await NextTipoHabIdAsync(),
                Estado = true
            };
            return IsAjax ? PartialView(model) : View(model);
        }

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("TipoHabitacionID,Nombre,Descripcion,Estado")] TipoHabitacion t)
        {
            // Genera ID si viene vacío o mal formado
            if (string.IsNullOrWhiteSpace(t.TipoHabitacionID) || !t.TipoHabitacionID.StartsWith("TIPO"))
                t.TipoHabitacionID = await NextTipoHabIdAsync();

            // Normaliza antes de validar/guardar
            t.Nombre = N(t.Nombre);
            t.Descripcion = N(t.Descripcion);
            t.Estado = true;

            if (!ModelState.IsValid)
                return IsAjax ? PartialView(nameof(Create), t) : View(nameof(Create), t);

            // Validación app: nombre único
            bool nombreTomado = await _db.tiposHabitacion.AnyAsync(x => x.Nombre == t.Nombre);
            if (nombreTomado)
            {
                ModelState.AddModelError(nameof(t.Nombre), "Ya existe un tipo de habitación con ese nombre.");
                return IsAjax ? PartialView(nameof(Create), t) : View(nameof(Create), t);
            }

            // (opcional) reintenta ID en caso de choque
            if (await _db.tiposHabitacion.AnyAsync(x => x.TipoHabitacionID == t.TipoHabitacionID))
                t.TipoHabitacionID = await NextTipoHabIdAsync();

            _db.Add(t);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception ex) when (IsUniqueViolation(ex))
            {
                // Respaldo por índice único en DB
                ModelState.AddModelError(nameof(t.Nombre), "Ya existe un tipo de habitación con ese nombre.");
                return IsAjax ? PartialView(nameof(Create), t) : View(nameof(Create), t);
            }

            if (IsAjax)
                return Json(new { ok = true, message = "¡Guardado exitosamente!", redirectUrl = Url.Action("Index") });

            return RedirectToAction(nameof(Index));
        }

        // ====== DETAILS ======
        [HttpGet("Details/{id}")]
        public async Task<IActionResult> Details(string id)
        {
            var t = await _db.tiposHabitacion.AsNoTracking().FirstOrDefaultAsync(x => x.TipoHabitacionID == id);
            if (t == null) return NotFound();
            return IsAjax ? PartialView(t) : View(t);
        }

        // ====== EDIT ======
        [HttpGet("Edit/{id}")]
        public async Task<IActionResult> Edit(string id)
        {
            var t = await _db.tiposHabitacion.AsNoTracking().FirstOrDefaultAsync(x => x.TipoHabitacionID == id);
            if (t == null) return NotFound();
            return IsAjax ? PartialView(t) : View(t);
        }

        [HttpPost("Edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("TipoHabitacionID,Nombre,Descripcion,Estado")] TipoHabitacion t)
        {
            if (id != t.TipoHabitacionID) return NotFound();

            // Normaliza antes de validar/guardar
            var nuevoNombre = N(t.Nombre);
            var nuevaDesc = N(t.Descripcion);

            if (!ModelState.IsValid)
                return IsAjax ? PartialView(nameof(Edit), t) : View(nameof(Edit), t);

            var db = await _db.tiposHabitacion.FirstOrDefaultAsync(x => x.TipoHabitacionID == id);
            if (db == null) return NotFound();

            // Validación app: nombre único excluyendo el propio registro
            bool nombreTomado = await _db.tiposHabitacion
                .AnyAsync(x => x.TipoHabitacionID != t.TipoHabitacionID && x.Nombre == nuevoNombre);
            if (nombreTomado)
            {
                ModelState.AddModelError(nameof(t.Nombre), "Ya existe un tipo de habitación con ese nombre.");
                return IsAjax ? PartialView(nameof(Edit), t) : View(nameof(Edit), t);
            }

            // Detectar cambios reales
            bool hayCambios =
                !string.Equals(N(db.Nombre), nuevoNombre, StringComparison.Ordinal) ||
                !string.Equals(N(db.Descripcion), nuevaDesc, StringComparison.Ordinal) ||
                db.Estado != t.Estado;

            if (!hayCambios)
            {
                if (IsAjax)
                    return Json(new { ok = false, reason = "nochanges", message = "Realiza un cambio antes de guardar." });

                ModelState.AddModelError(string.Empty, "Realiza un cambio antes de guardar.");
                return View(t);
            }

            // Aplicar cambios
            db.Nombre = nuevoNombre;
            db.Descripcion = nuevaDesc;
            db.Estado = t.Estado;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception ex) when (IsUniqueViolation(ex))
            {
                ModelState.AddModelError(nameof(t.Nombre), "Ya existe un tipo de habitación con ese nombre.");
                return IsAjax ? PartialView(nameof(Edit), t) : View(nameof(Edit), t);
            }

            if (IsAjax)
                return Json(new { ok = true, message = "¡Actualizado!", redirectUrl = Url.Action("Index") });

            return RedirectToAction(nameof(Edit), new { id = t.TipoHabitacionID });
        }

        // ====== DELETE (toggle estado) ======
        [HttpGet("Delete/{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var t = await _db.tiposHabitacion.AsNoTracking().FirstOrDefaultAsync(x => x.TipoHabitacionID == id);
            if (t == null) return NotFound();
            return IsAjax ? PartialView(t) : View(t);
        }


        // TiposHabitacionController.cs

        // [HttpPost("ToggleEstado/{id}")]   <-- QUITA esta línea
        [HttpPost("ToggleEstado")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleEstado([FromForm] string id)
        {
            var t = await _db.tiposHabitacion.FindAsync(id);
            if (t == null) return NotFound();

            t.Estado = !t.Estado;
            _db.Update(t);
            await _db.SaveChangesAsync();

            if (IsAjax)
            {
                var list = await _db.tiposHabitacion.AsNoTracking().ToListAsync();
                return PartialView(nameof(Index), list);
            }
            return RedirectToAction(nameof(Index));
        }


        // ====== PDF ======
        [HttpGet("ExportPdf")]
        public async Task<IActionResult> ExportPdf(string? codigo, string? nombre, string? estado, bool descargar = false)
        {
            var tieneEstado = Request.Query.ContainsKey("estado");
            if (!tieneEstado) estado = ""; // Todos

            var q = _db.tiposHabitacion.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(codigo)) q = q.Where(p => p.TipoHabitacionID.Contains(codigo));
            if (!string.IsNullOrWhiteSpace(nombre)) q = q.Where(p => p.Nombre.Contains(nombre));
            if (!string.IsNullOrWhiteSpace(estado))
                q = estado == "1" ? q.Where(p => p.Estado) : q.Where(p => !p.Estado);

            var model = await q.OrderBy(p => p.TipoHabitacionID).ToListAsync();

            ViewBag.Codigo = codigo; ViewBag.Nombre = nombre; ViewBag.Estado = estado;

            var pdf = new ViewAsPdf("ReportePdf", model)
            {
                PageSize = Size.A4,
                PageOrientation = Orientation.Portrait,
                CustomSwitches = "--footer-center \"Página [page] de [toPage]\" --footer-font-size 8 --footer-spacing 5",
                ContentDisposition = descargar ? ContentDisposition.Attachment : ContentDisposition.Inline
            };

            if (descargar) pdf.FileName = $"TiposHabitacion_{System.DateTime.Now:yyyyMMdd_HHmm}.pdf";
            return pdf;
        }

        // Normaliza texto (trim; si quieres *case-insensitive* forzado en app, usa .ToUpperInvariant())
        private static string N(string? s) => (s ?? "").Trim();

        // ¿La excepción es por UNIQUE/PK en SQL Server?
        private static bool IsUniqueViolation(Exception ex) =>
            ex is DbUpdateException dbu &&
            dbu.InnerException is SqlException sql &&
            (sql.Number == 2627 || sql.Number == 2601);


    }
}
