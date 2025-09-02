using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PIOGHOASIS.Infraestructure.Data;
using PIOGHOASIS.Models;
using Rotativa.AspNetCore.Options;
using Rotativa.AspNetCore;

namespace PIOGHOASIS.Controllers
{
    public class PuestosController : Controller
    {
        private readonly AppDbContext _context;
        public PuestosController(AppDbContext context) => _context = context;

        // helper para saber si la llamada viene desde fetch (dashboard)
        private bool IsAjax => Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        // ====== INDEX ======
        //public async Task<IActionResult> Index()
        //{
        //    var model = await _context.puestos.AsNoTracking().ToListAsync();
        //    return IsAjax ? PartialView(nameof(Index), model) : View(model);
        //}

        // ====== INDEX (con filtros) ======
        // ====== INDEX (con filtros y default: Activo) ======
        public async Task<IActionResult> Index(string? codigo, string? nombre, string? estado)
        {
            // Solo aplica default si el parámetro ni siquiera vino en la URL
            if (!Request.Query.ContainsKey("estado"))
                estado = "1";

            var q = _context.puestos.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(codigo))
                q = q.Where(p => p.PuestoID.Contains(codigo));

            if (!string.IsNullOrWhiteSpace(nombre))
                q = q.Where(p => p.Nombre.Contains(nombre));

            if (!string.IsNullOrWhiteSpace(estado))
            {
                if (estado == "1") q = q.Where(p => p.Estado);
                else if (estado == "0") q = q.Where(p => !p.Estado);
                // estado == ""  -> no entra al if => Todos
            }

            var model = await q.OrderBy(p => p.PuestoID).ToListAsync();
            return IsAjax ? PartialView(nameof(Index), model) : View(model);
        }


        // ====== DETAILS (opcional, mismo patrón AJAX) ======
        public async Task<IActionResult> Details(string id)
        {
            if (id == null) return NotFound();

            var puesto = await _context.puestos
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.PuestoID == id);

            if (puesto == null) return NotFound();
            return IsAjax ? PartialView(puesto) : View(puesto);
        }

        private async Task<string> NextPuestoIdAsync()
        {
            // Busca el mayor consecutivo numérico de los ids que empiezan con "PUESTO"
            var ids = await _context.puestos
                .AsNoTracking()
                .Where(x => x.PuestoID.StartsWith("PUESTO"))
                .Select(x => x.PuestoID)
                .ToListAsync();

            int max = 0;
            foreach (var id in ids)
            {
                // Toma solo la parte numérica al final (ej. "PUESTO00012" -> "00012")
                var digits = new string(id.SkipWhile(c => !char.IsDigit(c)).ToArray());
                if (int.TryParse(digits, out var n) && n > max) max = n;
            }

            var next = max + 1;                 // siguiente correlativo
            return $"PUESTO{next.ToString("D4")}"; // 5 dígitos con ceros a la izquierda
        }


        // GET: Puestos/Create
        public async Task<IActionResult> Create()
        {
            var model = new Puesto
            {
                PuestoID = await NextPuestoIdAsync(),
                Estado = true // para que el combo salga en "Activo"
            };
            return IsAjax ? PartialView(model) : View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("PuestoID,Nombre,Descripcion,Estado")] Puesto puesto)
        {
            if (!ModelState.IsValid)
                return IsAjax ? PartialView(puesto) : View(puesto);

            // Si no viene (o viene mal), genera uno
            if (string.IsNullOrWhiteSpace(puesto.PuestoID) || !puesto.PuestoID.StartsWith("PUESTO"))
                puesto.PuestoID = await NextPuestoIdAsync();

            // En caso raro de choque por concurrencia, vuelve a generar
            if (await _context.puestos.AnyAsync(x => x.PuestoID == puesto.PuestoID))
                puesto.PuestoID = await NextPuestoIdAsync();

            _context.Add(puesto);
            await _context.SaveChangesAsync();

            if (IsAjax)
                return Json(new
                {
                    ok = true,
                    message = "¡Puesto creado exitosamente!",
                    redirectUrl = Url.Action("Index", "Puestos")
                });

            return RedirectToAction(nameof(Index));
        }


        // ====== EDIT ======
        // GET
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null) return NotFound();

            var puesto = await _context.puestos.FindAsync(id);
            if (puesto == null) return NotFound();

            return IsAjax ? PartialView(puesto) : View(puesto);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, [Bind("PuestoID,Nombre,Descripcion,Estado")] Puesto puesto)
        {
            if (id != puesto.PuestoID) return NotFound();

            if (!ModelState.IsValid)
                return IsAjax ? PartialView(puesto) : View(puesto);

            // Cargar el registro actual (sin tracking para comparar)
            var actual = await _context.puestos
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.PuestoID == id);

            if (actual == null) return NotFound();

            // Normaliza espacios para evitar falsas diferencias
            string norm(string s) => (s ?? "").Trim();

            bool hayCambios =
                !string.Equals(norm(actual.Nombre), norm(puesto.Nombre), StringComparison.Ordinal) ||
                !string.Equals(norm(actual.Descripcion), norm(puesto.Descripcion), StringComparison.Ordinal) ||
                actual.Estado != puesto.Estado;

            if (!hayCambios)
            {
                if (IsAjax)
                    return Json(new
                    {
                        ok = false,
                        reason = "nochanges",
                        message = "Realiza un cambio antes de guardar."
                    });

                ModelState.AddModelError(string.Empty, "Realiza un cambio antes de guardar.");
                return View(puesto);
            }

            // Sí hay cambios: actualizar
            try
            {
                _context.Update(puesto);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.puestos.Any(e => e.PuestoID == puesto.PuestoID)) return NotFound();
                throw;
            }

            // AJAX: éxito => mostrará modal y (si quieres) redirige luego
            if (IsAjax)
                return Json(new
                {
                    ok = true,
                    message = "¡Puesto actualizado exitosamente!",
                    redirectUrl = Url.Action("Index", "Puestos") // si luego quieres ir a Index
                });

            // No AJAX: vuelve al mismo Edit
            return RedirectToAction(nameof(Edit), new { id = puesto.PuestoID });
        }

        // ====== DELETE ======
        // GET (confirmación)
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null) return NotFound();

            var puesto = await _context.puestos
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.PuestoID == id);

            if (puesto == null) return NotFound();
            return IsAjax ? PartialView(puesto) : View(puesto);
        }

        //// POST (confirmado)
        //[HttpPost, ActionName("Delete")]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> DeleteConfirmed(string id)
        //{
        //    var puesto = await _context.puestos.FindAsync(id);
        //    if (puesto != null)
        //    {
        //        _context.puestos.Remove(puesto);
        //        await _context.SaveChangesAsync();
        //    }

        //    if (IsAjax)
        //    {
        //        var list = await _context.puestos.AsNoTracking().ToListAsync();
        //        return PartialView(nameof(Index), list);
        //    }

        //    return RedirectToAction(nameof(Index));
        //}

        // POST: Puestos/ToggleEstado
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleEstado(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var puesto = await _context.puestos.FindAsync(id);
            if (puesto == null) return NotFound();

            // Cambiar estado
            puesto.Estado = !puesto.Estado;
            _context.Update(puesto);
            await _context.SaveChangesAsync();

            if (IsAjax)
            {
                var list = await _context.puestos.AsNoTracking().ToListAsync();
                return PartialView(nameof(Index), list);  // ← el Dashboard reemplaza #contentHost con esto
            }

            return RedirectToAction(nameof(Index));
        }

        private bool PuestoExists(string id)
            => _context.puestos.Any(e => e.PuestoID == id);

        ////Metodo para generación de pdf
        //[HttpGet]
        //public async Task<IActionResult> ExportPdf(string? codigo, string? nombre, string? estado)
        //{
        //    if (!Request.Query.ContainsKey("estado"))
        //        estado = "1";

        //    var q = _context.puestos.AsNoTracking().AsQueryable();

        //    if (!string.IsNullOrWhiteSpace(codigo))
        //        q = q.Where(p => p.PuestoID.Contains(codigo));

        //    if (!string.IsNullOrWhiteSpace(nombre))
        //        q = q.Where(p => p.Nombre.Contains(nombre));

        //    if (!string.IsNullOrWhiteSpace(estado))
        //    {
        //        if (estado == "1") q = q.Where(p => p.Estado);
        //        else if (estado == "0") q = q.Where(p => !p.Estado);
        //    }

        //    var model = await q.OrderBy(p => p.PuestoID).ToListAsync();

        //    // pasa también los filtros a la vista (para mostrarlos en el encabezado)
        //    ViewBag.Codigo = codigo;
        //    ViewBag.Nombre = nombre;
        //    ViewBag.Estado = estado;

        //    return new ViewAsPdf("ReportePdf", model)
        //    {
        //        FileName = $"Puestos_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
        //        PageSize = Size.A4,
        //        PageOrientation = Orientation.Portrait,
        //        CustomSwitches = "--footer-center \"Página [page] de [toPage]\" --footer-font-size 8 --footer-spacing 5"
        //    };
        //}

        [HttpGet]
        public async Task<IActionResult> ExportPdf(string? codigo, string? nombre, string? estado, bool descargar = false)
        {
            //if (!Request.Query.ContainsKey("estado")) estado = "1";
            var tieneEstado = Request.Query.ContainsKey("estado");
            if (!tieneEstado) estado = ""; // tratar como Todos

            var q = _context.puestos.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(codigo)) q = q.Where(p => p.PuestoID.Contains(codigo));
            if (!string.IsNullOrWhiteSpace(nombre)) q = q.Where(p => p.Nombre.Contains(nombre));
            if (!string.IsNullOrWhiteSpace(estado))
                q = estado == "1" ? q.Where(p => p.Estado) : q.Where(p => !p.Estado);

            var model = await q.OrderBy(p => p.PuestoID).ToListAsync();

            ViewBag.Codigo = codigo; ViewBag.Nombre = nombre; ViewBag.Estado = estado;

            var pdf = new ViewAsPdf("ReportePdf", model)
            {
                PageSize = Size.A4,
                PageOrientation = Orientation.Portrait,
                CustomSwitches = "--footer-center \"Página [page] de [toPage]\" --footer-font-size 8 --footer-spacing 5",
                ContentDisposition = descargar ? ContentDisposition.Attachment
                                               : ContentDisposition.Inline
            };

            if (descargar)
                pdf.FileName = $"Puestos_{DateTime.Now:yyyyMMdd_HHmm}.pdf"; // fuerza descarga

            return pdf; // si es Inline, el navegador lo previsualiza en una pestaña
        }
    }
}
