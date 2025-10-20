using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PIOGHOASIS.Infraestructure.Data;
using PIOGHOASIS.Models;
using Rotativa.AspNetCore;

namespace PIOGHOASIS.Controllers
{
    public class RolesController : Controller
    {
        private readonly AppDbContext _context;
        private bool IsAjax => Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        public RolesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: /Roles/Index
        public async Task<IActionResult> Index(string? codigo, string? nombre, string? estado)
        {
            // Por UX, si no vino 'estado' en la URL, arrancamos en "Activo"
            if (!Request.Query.ContainsKey("estado")) estado = "1";

            var q = _context.roles.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(codigo))
                q = q.Where(r => r.RolID.Contains(codigo));
            if (!string.IsNullOrWhiteSpace(nombre))
                q = q.Where(r => r.Nombre.Contains(nombre));

            // "" => Todos
            if (!string.IsNullOrWhiteSpace(estado))
            {
                if (estado == "1") q = q.Where(r => r.Estado);
                else if (estado == "0") q = q.Where(r => !r.Estado);
            }

            var model = await q.OrderBy(r => r.RolID).ToListAsync();
            return IsAjax ? PartialView(nameof(Index), model) : View(model);
        }

        private async Task<string> NextRolIdAsync()
        {
            // Si SIEMPRE guardas "ROL" + 7 dígitos (largo 10), el orden lexicográfico funciona perfecto.
            var lastId = await _context.roles.AsNoTracking()
                .Where(r => r.RolID.StartsWith("ROL"))
                .OrderByDescending(r => r.RolID)
                .Select(r => r.RolID)
                .FirstOrDefaultAsync();

            var lastNum = 0;
            if (!string.IsNullOrEmpty(lastId) && lastId.Length >= 4)
                int.TryParse(lastId.Substring(3), out lastNum);

            var next = lastNum + 1;
            return $"ROL{next:0000000}"; // ROL + 7 dígitos
        }

        // GET: /Roles/Create
        // GET: /Roles/Create
        public async Task<IActionResult> Create()
        {
            var model = new Rol
            {
                RolID = await NextRolIdAsync(), // <-- aquí
                Estado = true
            };
            return IsAjax ? PartialView(model) : View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Rol model)
        {
            if (string.IsNullOrWhiteSpace(model.RolID))
                model.RolID = await NextRolIdAsync();

            model.Nombre = N(model.Nombre); // normaliza
            model.Descripcion = N(model.Descripcion);
            model.Estado = true;

            if (!ModelState.IsValid)
                return IsAjax ? PartialView(nameof(Create), model) : View(nameof(Create), model);

            // Validación app: nombre único
            // (Si tu base es CI, con comparar == basta; si quieres forzar CI en app: ToUpper())
            bool nombreTomado = await _context.roles
                .AnyAsync(r => r.Nombre == model.Nombre);
            if (nombreTomado)
            {
                ModelState.AddModelError(nameof(model.Nombre), "Ya existe un rol con ese nombre.");
                return IsAjax ? PartialView(nameof(Create), model) : View(nameof(Create), model);
            }

            // (opcional) valida PK generada
            if (await _context.roles.AnyAsync(r => r.RolID == model.RolID))
            {
                model.RolID = await NextRolIdAsync(); // reintenta una vez
            }

            _context.Add(model);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex) when (IsUniqueViolation(ex))
            {
                // por si aún así chocamos con el UNIQUE
                ModelState.AddModelError(nameof(model.Nombre), "Ya existe un rol con ese nombre.");
                return IsAjax ? PartialView(nameof(Create), model) : View(nameof(Create), model);
            }

            return Json(new
            {
                ok = true,
                message = "Rol creado correctamente.",
                redirectUrl = Url.Action(nameof(Index), "Roles")
            });
        }

        //// POST: /Roles/Create
        //[HttpPost, ValidateAntiForgeryToken]
        //public async Task<IActionResult> Create(Rol model)
        //{
        //    if (string.IsNullOrWhiteSpace(model.RolID))
        //        model.RolID = await NextRolIdAsync(); // fallback

        //    if (!ModelState.IsValid)
        //        return IsAjax ? PartialView(nameof(Create), model) : View(nameof(Create), model);

        //    // Validación de PK duplicada
        //    var exists = await _context.roles.AnyAsync(r => r.RolID == model.RolID);
        //    if (exists)
        //    {
        //        // En altísima concurrencia podrías regenerar e intentar de nuevo:
        //        model.RolID = await NextRolIdAsync();
        //        exists = await _context.roles.AnyAsync(r => r.RolID == model.RolID);
        //        if (exists)
        //        {
        //            ModelState.AddModelError(nameof(model.RolID), "No se pudo generar un código único. Intenta de nuevo.");
        //            return IsAjax ? PartialView(nameof(Create), model) : View(nameof(Create), model);
        //        }
        //    }

        //    _context.Add(model);
        //    await _context.SaveChangesAsync();

        //    return Json(new
        //    {
        //        ok = true,
        //        message = "Rol creado correctamente.",
        //        redirectUrl = Url.Action(nameof(Index), "Roles")
        //    });
        //}

        // GET: /Roles/Edit/ROL0001
        public async Task<IActionResult> Edit(string id)
        {
            var model = await _context.roles.AsNoTracking()
                .FirstOrDefaultAsync(r => r.RolID == id);
            if (model == null) return NotFound();
            return IsAjax ? PartialView(model) : View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Rol model)
        {
            if (!ModelState.IsValid)
                return IsAjax ? PartialView(nameof(Edit), model) : View(nameof(Edit), model);

            var db = await _context.roles.FirstOrDefaultAsync(r => r.RolID == model.RolID);
            if (db == null) return NotFound();

            var nuevoNombre = N(model.Nombre);
            var nuevaDesc = N(model.Descripcion);

            // Validación app: nombre único (excluyendo el propio registro)
            bool nombreTomado = await _context.roles
                .AnyAsync(r => r.RolID != model.RolID && r.Nombre == nuevoNombre);
            if (nombreTomado)
            {
                ModelState.AddModelError(nameof(model.Nombre), "Ya existe un rol con ese nombre.");
                return IsAjax ? PartialView(nameof(Edit), model) : View(nameof(Edit), model);
            }

            var hadChanges =
                db.Nombre != nuevoNombre ||
                db.Descripcion != nuevaDesc ||
                db.Estado != model.Estado;

            db.Nombre = nuevoNombre;
            db.Descripcion = nuevaDesc;
            db.Estado = model.Estado;

            if (!hadChanges)
            {
                return Json(new
                {
                    ok = false,
                    reason = "nochanges",
                    message = "Realiza un cambio antes de guardar."
                });
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex) when (IsUniqueViolation(ex))
            {
                ModelState.AddModelError(nameof(model.Nombre), "Ya existe un rol con ese nombre.");
                return IsAjax ? PartialView(nameof(Edit), model) : View(nameof(Edit), model);
            }

            return Json(new
            {
                ok = true,
                message = "Rol actualizado.",
                redirectUrl = Url.Action(nameof(Index), "Roles")
            });
        }


        //// POST: /Roles/Edit
        //[HttpPost, ValidateAntiForgeryToken]
        //public async Task<IActionResult> Edit(Rol model)
        //{
        //    if (!ModelState.IsValid)
        //        return IsAjax ? PartialView(nameof(Edit), model) : View(nameof(Edit), model);

        //    var db = await _context.roles.FirstOrDefaultAsync(r => r.RolID == model.RolID);
        //    if (db == null) return NotFound();

        //    var hadChanges =
        //        db.Nombre != model.Nombre ||
        //        db.Descripcion != model.Descripcion ||
        //        db.Estado != model.Estado;

        //    db.Nombre = model.Nombre;
        //    db.Descripcion = model.Descripcion;
        //    db.Estado = model.Estado;

        //    if (!hadChanges)
        //    {
        //        return Json(new
        //        {
        //            ok = false,
        //            reason = "nochanges",
        //            message = "Realiza un cambio antes de guardar."
        //        });
        //    }

        //    await _context.SaveChangesAsync();

        //    return Json(new
        //    {
        //        ok = true,
        //        message = "Rol actualizado.",
        //        redirectUrl = Url.Action(nameof(Index), "Roles")
        //    });
        //}

        // GET: /Roles/Details/ROL0001
        public async Task<IActionResult> Details(string id)
        {
            var model = await _context.roles.AsNoTracking()
                .FirstOrDefaultAsync(r => r.RolID == id);
            if (model == null) return NotFound();
            return IsAjax ? PartialView(model) : View(model);
        }

        // GET: /Roles/Delete/ROL0001 (confirmación activar/desactivar)
        public async Task<IActionResult> Delete(string id)
        {
            var model = await _context.roles.AsNoTracking()
                .FirstOrDefaultAsync(r => r.RolID == id);
            if (model == null) return NotFound();
            return IsAjax ? PartialView(model) : View(model);
        }

        // POST: /Roles/Delete
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string RolID)
        {
            var model = await _context.roles.FirstOrDefaultAsync(r => r.RolID == RolID);
            if (model == null) return NotFound();

            model.Estado = !model.Estado; // toggle
            await _context.SaveChangesAsync();

            return Json(new
            {
                ok = true,
                message = model.Estado ? "Rol activado." : "Rol desactivado.",
                redirectUrl = Url.Action(nameof(Index), "Roles")
            });
        }

        // GET: /Roles/ExportPdf
        [HttpGet]
        public async Task<IActionResult> ExportPdf(string? codigo, string? nombre, string? estado)
        {
            // A diferencia del Index, NO forzamos default => "" = Todos
            var q = _context.roles.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(codigo))
                q = q.Where(r => r.RolID.Contains(codigo));
            if (!string.IsNullOrWhiteSpace(nombre))
                q = q.Where(r => r.Nombre.Contains(nombre));

            if (!string.IsNullOrWhiteSpace(estado))
            {
                if (estado == "1") q = q.Where(r => r.Estado);
                else if (estado == "0") q = q.Where(r => !r.Estado);
            }

            var model = await q.OrderBy(r => r.RolID).ToListAsync();

            // Si quieres abrir INLINE en el navegador, NO pongas FileName
            return new ViewAsPdf("ReportePdf", model)
            {
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,
                CustomSwitches = "--footer-center \"Página [page] de [toPage]\" --footer-font-size 8 --footer-spacing 5"
            };
        }

        //Validar si el rol ya existe
        // Devuelve true si la excepción proviene de UNIQUE/PRIMARY KEY en SQL Server
        private static bool IsUniqueViolation(Exception ex)
        {
            // 2627 = violación de UNIQUE/PK; 2601 = índice único duplicado
            return ex is DbUpdateException dbu &&
                   dbu.InnerException is SqlException sql &&
                   (sql.Number == 2627 || sql.Number == 2601);
        }

        // Normaliza texto para comparar (trim y opcional: ToUpper)
        private static string N(string? s) => (s ?? "").Trim();
    }
}
