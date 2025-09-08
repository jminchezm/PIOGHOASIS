using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PIOGHOASIS.Infraestructure.Data;
using PIOGHOASIS.Models;
using Rotativa.AspNetCore;

namespace PIOGHOASIS.Controllers
{
    public class TipoDocumentosController : Controller
    {
        private readonly AppDbContext _context;
        private bool IsAjax => Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        public TipoDocumentosController(AppDbContext context) => _context = context;

        // =============== Helpers ===============
        private async Task<string> GenerateNextCode(CancellationToken ct = default)
        {
            const string prefix = "TDOC"; // DOC0000001, DOC0000002...
            var last = await _context.tipoDocumentos.AsNoTracking()
                        .Where(x => x.TipoDocumentoID.StartsWith(prefix))
                        .OrderByDescending(x => x.TipoDocumentoID)
                        .Select(x => x.TipoDocumentoID)
                        .FirstOrDefaultAsync(ct);

            var n = 0;
            if (!string.IsNullOrEmpty(last) && last.Length >= prefix.Length + 1)
                int.TryParse(last.Substring(prefix.Length), out n);

            return $"{prefix}{(n + 1).ToString("D6")}";
        }

        // =============== Index ===============
        public async Task<IActionResult> Index(string? codigo, string? nombre, string? estado)
        {
            // UX: si no vino 'estado', arrancamos mostrando ACTIVOS
            if (!Request.Query.ContainsKey("estado")) estado = "1";

            var q = _context.tipoDocumentos.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(codigo))
                q = q.Where(t => t.TipoDocumentoID.Contains(codigo));
            if (!string.IsNullOrWhiteSpace(nombre))
                q = q.Where(t => t.Nombre.Contains(nombre));

            if (!string.IsNullOrWhiteSpace(estado))
            {
                if (estado == "1") q = q.Where(t => t.Estado);
                else if (estado == "0") q = q.Where(t => !t.Estado);
            }

            var model = await q.OrderBy(t => t.TipoDocumentoID).ToListAsync();
            return IsAjax ? PartialView(nameof(Index), model) : View(model);
        }

        // =============== Create ===============
        public async Task<IActionResult> Create()
        {
            var model = new TipoDocumento
            {
                TipoDocumentoID = await GenerateNextCode(),
                Estado = true
            };
            return IsAjax ? PartialView(model) : View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TipoDocumento model)
        {
            if (!ModelState.IsValid)
                return IsAjax ? PartialView(nameof(Create), model) : View(nameof(Create), model);

            // Si el usuario borró el código, volver a generarlo
            if (string.IsNullOrWhiteSpace(model.TipoDocumentoID))
                model.TipoDocumentoID = await GenerateNextCode();

            // PK duplicada
            var exists = await _context.tipoDocumentos.AnyAsync(t => t.TipoDocumentoID == model.TipoDocumentoID);
            if (exists)
            {
                ModelState.AddModelError(nameof(model.TipoDocumentoID), "El código ya existe.");
                return IsAjax ? PartialView(nameof(Create), model) : View(nameof(Create), model);
            }

            _context.Add(model);
            await _context.SaveChangesAsync();

            return Json(new
            {
                ok = true,
                message = "Tipo de documento creado correctamente.",
                redirectUrl = Url.Action(nameof(Index), "TipoDocumentos")
            });
        }

        // =============== Edit ===============
        public async Task<IActionResult> Edit(string id)
        {
            var model = await _context.tipoDocumentos.AsNoTracking()
                            .FirstOrDefaultAsync(t => t.TipoDocumentoID == id);
            if (model == null) return NotFound();
            return IsAjax ? PartialView(model) : View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(TipoDocumento model)
        {
            if (!ModelState.IsValid)
                return IsAjax ? PartialView(nameof(Edit), model) : View(nameof(Edit), model);

            var db = await _context.tipoDocumentos.FirstOrDefaultAsync(t => t.TipoDocumentoID == model.TipoDocumentoID);
            if (db == null) return NotFound();

            var hadChanges = db.Nombre != model.Nombre ||
                             //db.Descripcion != model.Descripcion ||
                             db.Estado != model.Estado;

            db.Nombre = model.Nombre;
            //db.Descripcion = model.Descripcion;
            db.Estado = model.Estado;

            if (!hadChanges)
            {
                return Json(new { ok = false, reason = "nochanges", message = "No has modificado ningún campo." });
            }

            await _context.SaveChangesAsync();

            return Json(new
            {
                ok = true,
                message = "Tipo de documento actualizado.",
                redirectUrl = Url.Action(nameof(Index), "TipoDocumentos")
            });
        }

        // =============== Details ===============
        public async Task<IActionResult> Details(string id)
        {
            var model = await _context.tipoDocumentos.AsNoTracking()
                            .FirstOrDefaultAsync(t => t.TipoDocumentoID == id);
            if (model == null) return NotFound();
            return IsAjax ? PartialView(model) : View(model);
        }

        // =============== Delete (toggle) ===============
        public async Task<IActionResult> Delete(string id)
        {
            var model = await _context.tipoDocumentos.AsNoTracking()
                            .FirstOrDefaultAsync(t => t.TipoDocumentoID == id);
            if (model == null) return NotFound();
            return IsAjax ? PartialView(model) : View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleEstado(string id)
        {
            var model = await _context.tipoDocumentos.FirstOrDefaultAsync(t => t.TipoDocumentoID == id);
            if (model == null) return NotFound();

            model.Estado = !model.Estado;
            await _context.SaveChangesAsync();

            return Json(new
            {
                ok = true,
                message = model.Estado ? "Tipo de documento activado." : "Tipo de documento desactivado.",
                redirectUrl = Url.Action(nameof(Index), "TipoDocumentos")
            });
        }

        // =============== Export PDF ===============
        [HttpGet]
        public async Task<IActionResult> ExportPdf(string? codigo, string? nombre, string? estado)
        {
            var q = _context.tipoDocumentos.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(codigo))
                q = q.Where(t => t.TipoDocumentoID.Contains(codigo));
            if (!string.IsNullOrWhiteSpace(nombre))
                q = q.Where(t => t.Nombre.Contains(nombre));

            if (!string.IsNullOrWhiteSpace(estado))
            {
                if (estado == "1") q = q.Where(t => t.Estado);
                else if (estado == "0") q = q.Where(t => !t.Estado);
            }

            var model = await q.OrderBy(t => t.TipoDocumentoID).ToListAsync();

            return new ViewAsPdf("ReportePdf", model)
            {
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,
                CustomSwitches = "--footer-center \"Página [page] de [toPage]\" --footer-font-size 8 --footer-spacing 5"
            };
        }
    }
}
