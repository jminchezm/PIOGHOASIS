using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using PIOGHOASIS.Infraestructure.Data;
using PIOGHOASIS.Models;
using PIOGHOASIS.Models.ViewModels;
using Rotativa.AspNetCore;
using Rotativa.AspNetCore.Options;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PIOGHOASIS.Controllers
{
    [Route("Habitaciones")]
    public class HabitacionesController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        public HabitacionesController(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db; _env = env;
        }

        private bool IsAjax => Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        // ========= LISTA =========
        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index(string? codigo, string? tipo, int? personas, string? estado)
        {
            if (!Request.Query.ContainsKey("estado")) estado = ""; // Todos

            var q = _db.habitaciones
                .Include(h => h.TipoHabitacion)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(codigo))
                q = q.Where(h => h.Codigo.Contains(codigo));

            if (!string.IsNullOrWhiteSpace(tipo))
                q = q.Where(h => h.TipoHabitacionID == tipo);

            if (personas.HasValue && personas.Value > 0)
                q = q.Where(h => h.CapacidadPersonas == personas.Value);

            if (!string.IsNullOrWhiteSpace(estado))
            {
                if (estado == "1") q = q.Where(h => h.Estado);
                else if (estado == "0") q = q.Where(h => !h.Estado);
            }

            var model = await q.OrderBy(h => h.Codigo).ToListAsync();
            ViewBag.Tipos = await _db.tiposHabitacion.AsNoTracking().OrderBy(t => t.Nombre).ToListAsync();

            return IsAjax ? PartialView(nameof(Index), model) : View(model);
        }

        // ========= UTILS =========
        private async Task<string> NextCodigoAsync()
        {
            var cods = await _db.habitaciones.AsNoTracking().Select(x => x.Codigo).ToListAsync();
            int max = 0;
            foreach (var c in cods)
            {
                var digits = new string((c ?? "").SkipWhile(ch => !char.IsDigit(ch)).ToArray());
                if (int.TryParse(digits, out var n) && n > max) max = n;
            }
            return $"HAB{(max + 1).ToString("D3")}";
        }

        private async Task CargarTiposAsync()
        {
            ViewBag.Tipos = await _db.tiposHabitacion.AsNoTracking().OrderBy(t => t.Nombre).ToListAsync();
        }

        private async Task<string?> GuardarImagenAsync(IFormFile? file)
        {
            if (file == null || file.Length == 0) return null;

            var folder = Path.Combine(_env.WebRootPath, "uploads", "habitaciones");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            var filename = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName)}";
            var path = Path.Combine(folder, filename);

            using (var fs = new FileStream(path, FileMode.Create))
                await file.CopyToAsync(fs);

            // ruta relativa para <img src="">
            return $"/uploads/habitaciones/{filename}";
        }

        // ========= CREATE =========

        [HttpGet("Create")]
        public async Task<IActionResult> Create()
        {
            await CargarTiposAsync();

            var vm = new HabitacionCreateVM
            {
                Habitacion = new Habitacion
                {
                    Codigo = await NextCodigoAsync(),
                    Estado = true
                },
                TarifaItems = new List<TarifaItemVM>
        {
            new TarifaItemVM
            {
                NumeroPersonas = 1,
                FechaInicio = DateTime.Today,
                FechaFin = DateTime.Today.AddMonths(3),
                PrecioNocheStr = "" // usuario llena
            }
        }
            };

            return IsAjax ? PartialView(vm) : View(vm);
        }

        //[HttpGet("Create")]
        //public async Task<IActionResult> Create()
        //{
        //    await CargarTiposAsync();
        //    var m = new Habitacion
        //    {
        //        Codigo = await NextCodigoAsync(),
        //        Estado = true
        //    };
        //    return IsAjax ? PartialView(m) : View(m);
        //}

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(HabitacionCreateVM vm, IFormFile? fileImagen)
        {
            // Validación básica del modelo Habitacion
            if (!ModelState.IsValid)
            {
                await CargarTiposAsync();
                return IsAjax ? PartialView(vm) : View(vm);
            }

            // Normaliza código
            if (string.IsNullOrWhiteSpace(vm.Habitacion.Codigo) || !vm.Habitacion.Codigo.StartsWith("HAB"))
                vm.Habitacion.Codigo = await NextCodigoAsync();
            if (await _db.habitaciones.AnyAsync(x => x.Codigo == vm.Habitacion.Codigo))
                vm.Habitacion.Codigo = await NextCodigoAsync();

            // Imagen
            if (fileImagen != null)
                vm.Habitacion.Imagen = await GuardarImagenAsync(fileImagen);

            // Validaciones de tarifas
            if (vm.TarifaItems == null || vm.TarifaItems.Count == 0)
                ModelState.AddModelError("", "Debe agregar al menos una tarifa.");

            // Normalizar y convertir precios
            var tarifas = new List<TarifaHabitacion>();
            if (vm.TarifaItems != null)
            {
                foreach (var t in vm.TarifaItems)
                {
                    if (t.FechaFin < t.FechaInicio)
                        ModelState.AddModelError("", $"Rango de fechas inválido ({t.FechaInicio:yyyy-MM-dd} – {t.FechaFin:yyyy-MM-dd}).");

                    if (vm.Habitacion.CapacidadPersonas.HasValue && t.NumeroPersonas > vm.Habitacion.CapacidadPersonas.Value)
                        ModelState.AddModelError("", $"La ocupación {t.NumeroPersonas} supera la capacidad de la habitación.");

                    // quitar comas de miles y convertir a decimal con punto
                    var limpio = (t.PrecioNocheStr ?? "").Replace(",", "");
                    if (!decimal.TryParse(limpio, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var precio))
                        ModelState.AddModelError("", "Precio inválido en una de las filas de tarifas.");
                    else if (precio <= 0)
                        ModelState.AddModelError("", "El precio debe ser mayor a 0.");

                    tarifas.Add(new TarifaHabitacion
                    {
                        NumeroPersonas = t.NumeroPersonas,
                        PrecioNoche = precio,
                        FechaInicio = t.FechaInicio.Date,
                        FechaFin = t.FechaFin.Date,
                        EtiquetaTemporada = t.EtiquetaTemporada
                    });
                }
            }

            // Si hubo errores en tarifas, volver a la vista
            if (!ModelState.IsValid)
            {
                await CargarTiposAsync();
                return IsAjax ? PartialView(vm) : View(vm);
            }

            // Persistencia: primero habitación, luego tarifas (con FK)
            _db.habitaciones.Add(vm.Habitacion);
            await _db.SaveChangesAsync();

            foreach (var t in tarifas)
                t.HabitacionID = vm.Habitacion.HabitacionID;

            // Validación anti-solape simple en servidor (opcional, además del trigger)
            foreach (var t in tarifas)
            {
                var overlap = await _db.tarifasHabitacion
                    .AnyAsync(x => x.HabitacionID == t.HabitacionID
                                && x.NumeroPersonas == t.NumeroPersonas
                                && !(t.FechaFin < x.FechaInicio || t.FechaInicio > x.FechaFin));
                if (overlap)
                {
                    ModelState.AddModelError("", $"Ya existe una tarifa que se solapa (ocupación {t.NumeroPersonas}) en el rango {t.FechaInicio:yyyy-MM-dd} a {t.FechaFin:yyyy-MM-dd}.");
                    await CargarTiposAsync();
                    return IsAjax ? PartialView(vm) : View(vm);
                }
            }

            _db.tarifasHabitacion.AddRange(tarifas);
            await _db.SaveChangesAsync();

            if (IsAjax)
                return Json(new { ok = true, message = "¡Habitación y tarifas creadas!", redirectUrl = Url.Action("Index") });

            return RedirectToAction(nameof(Index));
        }


        //[HttpPost("Create")]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Create(Habitacion h, IFormFile? fileImagen)
        //{
        //    if (!ModelState.IsValid)
        //    {
        //        await CargarTiposAsync();
        //        return IsAjax ? PartialView(h) : View(h);
        //    }

        //    if (string.IsNullOrWhiteSpace(h.Codigo) || !h.Codigo.StartsWith("HAB"))
        //        h.Codigo = await NextCodigoAsync();

        //    if (await _db.habitaciones.AnyAsync(x => x.Codigo == h.Codigo))
        //        h.Codigo = await NextCodigoAsync();

        //    if (fileImagen != null)
        //        h.Imagen = await GuardarImagenAsync(fileImagen);

        //    _db.Add(h);
        //    await _db.SaveChangesAsync();

        //    if (IsAjax)
        //        return Json(new { ok = true, message = "¡Habitación creada!", redirectUrl = Url.Action("Index") });

        //    return RedirectToAction(nameof(Index));
        //}

        // ========= DETAILS =========

        // ========= DETAILS =========
        [HttpGet("Details/{id:int}")]
        public async Task<IActionResult> Details(int id)
        {
            var m = await _db.habitaciones
                .Include(x => x.TipoHabitacion)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.HabitacionID == id);

            if (m == null) return NotFound();

            // Cargar tarifas de esta habitación
            var tarifas = await _db.tarifasHabitacion
                .Where(t => t.HabitacionID == id)
                .OrderBy(t => t.NumeroPersonas)
                .ThenBy(t => t.FechaInicio)
                .AsNoTracking()
                .ToListAsync();

            ViewBag.Tarifas = tarifas;

            return IsAjax ? PartialView(m) : View(m);
        }


        //[HttpGet("Details/{id:int}")]
        //public async Task<IActionResult> Details(int id)
        //{
        //    var m = await _db.habitaciones
        //        .Include(x => x.TipoHabitacion)
        //        .AsNoTracking()
        //        .FirstOrDefaultAsync(x => x.HabitacionID == id);

        //    if (m == null) return NotFound();
        //    return IsAjax ? PartialView(m) : View(m);
        //}

        // ========= EDIT =========

        [HttpGet("Edit/{id:int}")]
        public async Task<IActionResult> Edit(int id)
        {
            var hab = await _db.habitaciones.FindAsync(id);
            if (hab == null) return NotFound();

            await CargarTiposAsync();

            var tarifas = await _db.tarifasHabitacion
                .Where(t => t.HabitacionID == id)
                .OrderBy(t => t.NumeroPersonas).ThenBy(t => t.FechaInicio)
                .AsNoTracking()
                .ToListAsync();

            var vm = new HabitacionCreateVM
            {
                Habitacion = hab,
                TarifaItems = tarifas.Select(t => new TarifaItemVM
                {
                    NumeroPersonas = t.NumeroPersonas,
                    FechaInicio = t.FechaInicio,
                    FechaFin = t.FechaFin,
                    EtiquetaTemporada = t.EtiquetaTemporada,
                    // mostramos el precio formateado como string con punto decimal
                    PrecioNocheStr = t.PrecioNoche.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                }).ToList()
            };

            // Si no hay tarifas aún, deja una fila vacía
            if (vm.TarifaItems.Count == 0)
            {
                vm.TarifaItems.Add(new TarifaItemVM
                {
                    NumeroPersonas = 1,
                    FechaInicio = DateTime.Today,
                    FechaFin = DateTime.Today.AddMonths(3)
                });
            }

            return IsAjax ? PartialView(vm) : View(vm);
        }


        //[HttpGet("Edit/{id:int}")]
        //public async Task<IActionResult> Edit(int id)
        //{
        //    var m = await _db.habitaciones.FindAsync(id);
        //    if (m == null) return NotFound();
        //    await CargarTiposAsync();
        //    return IsAjax ? PartialView(m) : View(m);
        //}

        [HttpPost("Edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, HabitacionCreateVM vm, IFormFile? fileImagen, bool QuitarImagen = false)
        {
            if (id != vm.Habitacion.HabitacionID) return NotFound();

            // validación de la parte Habitacion
            if (!ModelState.IsValid)
            {
                await CargarTiposAsync();
                return IsAjax ? PartialView(vm) : View(vm);
            }

            // Cargar el estado actual para manejar la imagen
            var actual = await _db.habitaciones.AsNoTracking()
                            .FirstOrDefaultAsync(x => x.HabitacionID == id);
            if (actual == null) return NotFound();

            // Imagen (igual que ya lo hacías)
            if (QuitarImagen)
            {
                if (!string.IsNullOrWhiteSpace(actual.Imagen))
                {
                    var physical = Path.Combine(_env.WebRootPath, actual.Imagen.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(physical))
                        System.IO.File.Delete(physical);
                }
                vm.Habitacion.Imagen = null;
            }
            else if (fileImagen != null)
            {
                vm.Habitacion.Imagen = await GuardarImagenAsync(fileImagen);
            }
            else
            {
                vm.Habitacion.Imagen = actual.Imagen; // conservar
            }

            // ===== VALIDACIÓN Y CONVERSIÓN DE TARIFAS =====
            if (vm.TarifaItems == null || vm.TarifaItems.Count == 0)
                ModelState.AddModelError("", "Debe agregar al menos una tarifa.");

            var nuevasTarifas = new List<TarifaHabitacion>();

            if (vm.TarifaItems != null)
            {
                foreach (var t in vm.TarifaItems)
                {
                    if (t.FechaFin < t.FechaInicio)
                        ModelState.AddModelError("", $"Rango inválido ({t.FechaInicio:yyyy-MM-dd} – {t.FechaFin:yyyy-MM-dd}).");

                    if (vm.Habitacion.CapacidadPersonas.HasValue && t.NumeroPersonas > vm.Habitacion.CapacidadPersonas.Value)
                        ModelState.AddModelError("", $"La ocupación {t.NumeroPersonas} supera la capacidad de la habitación.");

                    var limpio = (t.PrecioNocheStr ?? "").Replace(",", "");
                    if (!decimal.TryParse(limpio, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var precio))
                        ModelState.AddModelError("", "Precio inválido en una de las filas de tarifas.");
                    else if (precio <= 0)
                        ModelState.AddModelError("", "El precio debe ser mayor a 0.");

                    nuevasTarifas.Add(new TarifaHabitacion
                    {
                        HabitacionID = id,
                        NumeroPersonas = t.NumeroPersonas,
                        PrecioNoche = precio,
                        FechaInicio = t.FechaInicio.Date,
                        FechaFin = t.FechaFin.Date,
                        EtiquetaTemporada = t.EtiquetaTemporada
                    });
                }
            }

            if (!ModelState.IsValid)
            {
                await CargarTiposAsync();
                return IsAjax ? PartialView(vm) : View(vm);
            }

            // ===== Persistencia en transacción =====
            using var tx = await _db.Database.BeginTransactionAsync();

            try
            {
                // actualizar la habitación
                _db.Entry(vm.Habitacion).State = EntityState.Modified;
                await _db.SaveChangesAsync();

                // eliminar tarifas anteriores
                var old = await _db.tarifasHabitacion.Where(x => x.HabitacionID == id).ToListAsync();
                _db.tarifasHabitacion.RemoveRange(old);
                await _db.SaveChangesAsync();

                // (opcional) validación anti-solape entre nuevas
                // puedes chequear aquí que en 'nuevasTarifas' no haya traslapes por (NumeroPersonas)
                bool solape = nuevasTarifas
                    .GroupBy(t => t.NumeroPersonas)
                    .Any(g => g.Select(x => (x.FechaInicio, x.FechaFin))
                               .OrderBy(x => x.FechaInicio)
                               .Zip(g.Select(x => (x.FechaInicio, x.FechaFin))
                                     .OrderBy(x => x.FechaInicio).Skip(1),
                                    (a, b) => a.FechaFin >= b.FechaInicio).Any(s => s));
                if (solape)
                {
                    await tx.RollbackAsync();
                    ModelState.AddModelError("", "Hay tarifas nuevas que se solapan en fechas para la misma ocupación.");
                    await CargarTiposAsync();
                    return IsAjax ? PartialView(vm) : View(vm);
                }

                // insertar nuevas
                _db.tarifasHabitacion.AddRange(nuevasTarifas);
                await _db.SaveChangesAsync();

                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }

            if (IsAjax) return Json(new { ok = true, message = "¡Habitación y tarifas actualizadas!", redirectUrl = Url.Action("Index") });
            return RedirectToAction(nameof(Edit), new { id });
        }


        //[HttpPost("Edit/{id:int}")]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Edit(int id, Habitacion h, IFormFile? fileImagen, bool QuitarImagen = false)
        //{
        //    if (id != h.HabitacionID) return NotFound();
        //    if (!ModelState.IsValid) { await CargarTiposAsync(); return IsAjax ? PartialView(h) : View(h); }

        //    var actual = await _db.habitaciones.AsNoTracking().FirstOrDefaultAsync(x => x.HabitacionID == id);
        //    if (actual == null) return NotFound();

        //    //if (QuitarImagen)
        //    //    h.Imagen = null;              // opcional: también borra el archivo físico si quieres
        //    if (QuitarImagen)
        //    {
        //        // borrar archivo físico si existía
        //        if (!string.IsNullOrWhiteSpace(actual.Imagen))
        //        {
        //            var physical = Path.Combine(_env.WebRootPath, actual.Imagen.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        //            if (System.IO.File.Exists(physical))
        //                System.IO.File.Delete(physical);
        //        }

        //        h.Imagen = null;
        //    }
        //    else if (fileImagen != null)
        //    {
        //        h.Imagen = await GuardarImagenAsync(fileImagen);
        //    }
        //    else
        //    {
        //        h.Imagen = actual.Imagen;
        //    }
        //    _db.Update(h);
        //    await _db.SaveChangesAsync();

        //    if (IsAjax) return Json(new { ok = true, message = "¡Habitación actualizada!", redirectUrl = Url.Action("Index") });
        //    return RedirectToAction(nameof(Edit), new { id = h.HabitacionID });
        //}

        // ========= DELETE (Toggle) =========
        [HttpGet("Delete/{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var m = await _db.habitaciones
                .Include(x => x.TipoHabitacion)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.HabitacionID == id);

            if (m == null) return NotFound();
            return IsAjax ? PartialView(m) : View(m);
        }

        [HttpPost("ToggleEstado/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleEstado(int id)
        {
            var h = await _db.habitaciones.FindAsync(id);
            if (h == null) return NotFound();

            h.Estado = !h.Estado;
            _db.Update(h);
            await _db.SaveChangesAsync();

            if (IsAjax)
            {
                var list = await _db.habitaciones.Include(x => x.TipoHabitacion).AsNoTracking().ToListAsync();
                ViewBag.Tipos = await _db.tiposHabitacion.AsNoTracking().OrderBy(t => t.Nombre).ToListAsync();
                return PartialView(nameof(Index), list);
            }
            return RedirectToAction(nameof(Index));
        }

        // ========= PDF =========

        [HttpGet("ExportPdf")]
        public async Task<IActionResult> ExportPdf(string? codigo, string? tipo, int? personas, string? estado, bool descargar = false)
        {
            var tieneEstado = Request.Query.ContainsKey("estado");
            if (!tieneEstado) estado = "";

            var q = _db.habitaciones.Include(h => h.TipoHabitacion).AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(codigo)) q = q.Where(h => h.Codigo.Contains(codigo));
            if (!string.IsNullOrWhiteSpace(tipo)) q = q.Where(h => h.TipoHabitacionID == tipo);
            if (personas.HasValue && personas > 0) q = q.Where(h => h.CapacidadPersonas == personas);
            if (!string.IsNullOrWhiteSpace(estado))
                q = estado == "1" ? q.Where(h => h.Estado) : q.Where(h => !h.Estado);

            var model = await q.OrderBy(h => h.Codigo).ToListAsync();

            // tarifas por habitación
            var ids = model.Select(h => h.HabitacionID).ToList();
            var tarifas = await _db.tarifasHabitacion
                .Where(t => ids.Contains(t.HabitacionID))
                .OrderBy(t => t.HabitacionID).ThenBy(t => t.NumeroPersonas).ThenBy(t => t.FechaInicio)
                .AsNoTracking()
                .ToListAsync();

            var tarifasPorHab = tarifas
                .GroupBy(t => t.HabitacionID)
                .ToDictionary(g => g.Key, g => (IEnumerable<TarifaHabitacion>)g.ToList());

            // Construir ViewData para el PDF
            var vdd = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
            {
                Model = model
            };
            vdd["TarifasPorHab"] = tarifasPorHab;

            var pdf = new ViewAsPdf("ReportePdf", model)
            {
                PageSize = Size.A4,
                PageOrientation = Orientation.Landscape,
                CustomSwitches = "--footer-center \"Página [page] de [toPage]\" --footer-font-size 8 --footer-spacing 5",
                ContentDisposition = descargar ? ContentDisposition.Attachment : ContentDisposition.Inline,
                ViewData = vdd // 👈 PASAMOS ViewData a la vista del PDF
            };
            if (descargar) pdf.FileName = $"Habitaciones_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
            return pdf;
        }


        //[HttpGet("ExportPdf")]
        //public async Task<IActionResult> ExportPdf(string? codigo, string? tipo, int? personas, string? estado, bool descargar = false)
        //{
        //    var tieneEstado = Request.Query.ContainsKey("estado");
        //    if (!tieneEstado) estado = "";

        //    var q = _db.habitaciones.Include(h => h.TipoHabitacion).AsNoTracking().AsQueryable();
        //    if (!string.IsNullOrWhiteSpace(codigo)) q = q.Where(h => h.Codigo.Contains(codigo));
        //    if (!string.IsNullOrWhiteSpace(tipo)) q = q.Where(h => h.TipoHabitacionID == tipo);
        //    if (personas.HasValue && personas > 0) q = q.Where(h => h.CapacidadPersonas == personas);
        //    if (!string.IsNullOrWhiteSpace(estado))
        //        q = estado == "1" ? q.Where(h => h.Estado) : q.Where(h => !h.Estado);

        //    var model = await q.OrderBy(h => h.Codigo).ToListAsync();

        //    ViewBag.Codigo = codigo;
        //    ViewBag.Tipo = tipo;
        //    ViewBag.Personas = personas;
        //    ViewBag.Estado = estado;


        //    // IDs de las habitaciones del reporte
        //    var ids = model.Select(h => h.HabitacionID).ToList();

        //    // Traer todas las tarifas de esas habitaciones
        //    var tarifas = await _db.tarifasHabitacion
        //        .Where(t => ids.Contains(t.HabitacionID))
        //        .OrderBy(t => t.HabitacionID)
        //        .ThenBy(t => t.NumeroPersonas)
        //        .ThenBy(t => t.FechaInicio)
        //        .AsNoTracking()
        //        .ToListAsync();

        //    // Agrupar en un diccionario: HabitaciónID => IEnumerable<TarifaHabitacion>
        //    var tarifasPorHab = tarifas
        //        .GroupBy(t => t.HabitacionID)
        //        .ToDictionary(g => g.Key, g => (IEnumerable<TarifaHabitacion>)g.ToList());

        //    // Pasarlo a la vista (lo que lee ReportePdf.cshtml)
        //    ViewBag.TarifasPorHab = tarifasPorHab;

        //    var pdf = new ViewAsPdf("ReportePdf", model)
        //    {
        //        PageSize = Size.A4,
        //        PageOrientation = Orientation.Landscape,
        //        CustomSwitches = "--footer-center \"Página [page] de [toPage]\" --footer-font-size 8 --footer-spacing 5",
        //        ContentDisposition = descargar ? ContentDisposition.Attachment : ContentDisposition.Inline
        //    };
        //    if (descargar) pdf.FileName = $"Habitaciones_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
        //    return pdf;
        //}
    }
}