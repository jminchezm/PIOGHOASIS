using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PIOGHOASIS.Infraestructure.Data;
using PIOGHOASIS.Models;
using PIOGHOASIS.Models.ViewModels;
using Rotativa.AspNetCore;
using System.Globalization;

namespace PIOGHOASIS.Controllers
{
    public class EmpleadosController : Controller
    {
        private readonly AppDbContext _context;
        private bool IsAjax => Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        private readonly IWebHostEnvironment _env;
        private const string SqlCollation = "SQL_Latin1_General_CP1_CI_AI";

        public EmpleadosController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // ===== Utilidades SOLO SQL SERVER =====
        private static DateTime? ParseIso(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            return DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                          DateTimeStyles.None, out var d) ? d.Date : null;
        }

        /// <summary>
        /// Aplica todos los filtros (código, persona, puesto, estado, fechas) usando SOLO LIKE (SQL Server).
        /// </summary>
        private IQueryable<Empleado> FiltrarEmpleadosSqlServer(
        IQueryable<Empleado> q,
        string? codigo, string? persona, string? puesto, string? estado,
        string? fIniStr, string? fFinStr)
        {
            // Código (funcionaba)
            if (!string.IsNullOrWhiteSpace(codigo))
                q = q.Where(e => e.EmpleadoID.Contains(codigo));

            // Persona: nombre completo o PersonaID (FORZAMOS CI_AI)
            if (!string.IsNullOrWhiteSpace(persona))
            {
                var term = persona.Trim();

                q = q.Where(e =>
                    EF.Functions.Like(
                        EF.Functions.Collate(
                            ((e.Persona.PrimerNombre ?? "") + " " +
                             (e.Persona.SegundoNombre ?? "") + " " +
                             (e.Persona.PrimerApellido ?? "") + " " +
                             (e.Persona.SegundoApellido ?? "")),
                            SqlCollation),
                        $"%{term}%")
                    ||
                    EF.Functions.Like(
                        EF.Functions.Collate((e.PersonalID ?? ""), SqlCollation),
                        $"%{term}%")
                );
            }

            // Puesto: nombre o código (FORZAMOS CI_AI)
            if (!string.IsNullOrWhiteSpace(puesto))
            {
                var term = puesto.Trim();

                q = q.Where(e =>
                    EF.Functions.Like(EF.Functions.Collate((e.Puesto.Nombre ?? ""), SqlCollation), $"%{term}%")
                    || EF.Functions.Like(EF.Functions.Collate((e.PuestoID ?? ""), SqlCollation), $"%{term}%")
                );
            }

            // Estado
            if (!string.IsNullOrWhiteSpace(estado))
            {
                if (estado == "1") q = q.Where(e => e.Estado);
                else if (estado == "0") q = q.Where(e => !e.Estado);
            }

            // Fechas (rango inclusivo) -> fin < fFin + 1 día
            static DateTime? ParseIsoLocal(string? s)
                => string.IsNullOrWhiteSpace(s) ? null
                   : (DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d.Date : null);

            var ini = ParseIsoLocal(fIniStr);
            var fin = ParseIsoLocal(fFinStr);
            if (ini.HasValue) q = q.Where(e => e.FechaContratacion >= ini.Value);
            if (fin.HasValue) q = q.Where(e => e.FechaContratacion < fin.Value.AddDays(1));

            return q;
        }

        // ===== INDEX =====
        [HttpGet]
        public async Task<IActionResult> Index(
            [FromQuery] string? codigo,
            [FromQuery] string? persona,
            [FromQuery] string? puesto,
            [FromQuery] string? estado,
            [FromQuery(Name = "fIni")] string? fIniStr,
            [FromQuery(Name = "fFin")] string? fFinStr)
        {
            if (!Request.Query.ContainsKey("estado")) estado = "1";

            var q = _context.empleados
                .AsNoTracking()
                .Include(e => e.Persona)
                .Include(e => e.Puesto)
                .AsQueryable();

            q = FiltrarEmpleadosSqlServer(q, codigo, persona, puesto, estado, fIniStr, fFinStr);

            var model = await q.OrderBy(e => e.EmpleadoID).ToListAsync();
            ViewBag.IsPartial = IsAjax;
            return IsAjax ? PartialView(nameof(Index), model) : View(model);
        }

        // ===== Utilidades de correlativos =====
        private async Task<string> NextEmpleadoIdAsync()
        {
            var last = await _context.empleados
                .OrderByDescending(e => e.EmpleadoID)
                .Select(e => e.EmpleadoID)
                .FirstOrDefaultAsync();

            int n = 0;
            if (!string.IsNullOrEmpty(last) && last.StartsWith("EMP") && last.Length == 10)
                int.TryParse(last.Substring(3), out n);

            // EMP + 7 dígitos -> 10 chars
            return $"EMP{(n + 1):0000000}";
        }

        private async Task<string> NextPersonaIdAsync()
        {
            var last = await _context.personas
                .OrderByDescending(p => p.PersonaID)
                .Select(p => p.PersonaID)
                .FirstOrDefaultAsync();

            int n = 0;
            if (!string.IsNullOrEmpty(last) && last.StartsWith("PER") && last.Length == 10)
                int.TryParse(last.Substring(3), out n);

            // PER + 7 dígitos -> 10 chars
            return $"PER{(n + 1):0000000}";
        }

        private static string NombreCompleto(Persona p) =>
            string.Join(" ", new[] { p.PrimerNombre, p.SegundoNombre, p.PrimerApellido, p.SegundoApellido }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

        private async Task CargarCombosPuestosAsync(string? puestoId = null)
        {
            var puestos = await _context.puestos
                .OrderBy(p => p.PuestoID)
                .Select(p => new SelectListItem
                {
                    Value = p.PuestoID,
                    Text = p.PuestoID + " - " + p.Nombre
                }).ToListAsync();

            ViewBag.Puestos = new SelectList(puestos, "Value", "Text", puestoId);
        }

        private async Task CargarCombosCreateAsync(
            string? puestoId = null,
            string? tipoDocumentoId = null,
            string? paisId = null,
            int? deptoId = null,
            int? municipioId = null)
        {
            // Puestos
            var puestos = await _context.puestos
                .Where(p => p.Estado)
                .OrderBy(p => p.PuestoID)
                .Select(p => new SelectListItem { Value = p.PuestoID, Text = p.PuestoID + " - " + p.Nombre })
                .ToListAsync();
            ViewBag.Puestos = new SelectList(puestos, "Value", "Text", puestoId);

            // Tipos de documento
            var tdocs = await _context.tipoDocumentos
                .Where(t => t.Estado)
                .OrderBy(t => t.TipoDocumentoID)
                .Select(t => new SelectListItem { Value = t.TipoDocumentoID, Text = t.TipoDocumentoID + " - " + t.Nombre })
                .ToListAsync();
            ViewBag.TiposDocumento = new SelectList(tdocs, "Value", "Text", tipoDocumentoId);

            // Países
            var paises = await _context.paises
                .Where(p => p.Estado)
                .OrderBy(p => p.PaisID)
                .Select(p => new SelectListItem { Value = p.PaisID, Text = p.PaisID + " - " + p.Nombre })
                .ToListAsync();
            ViewBag.Paises = new SelectList(paises, "Value", "Text", paisId);

            // Departamentos (si GTM)
            var dptos = new List<SelectListItem>();
            if (!string.IsNullOrWhiteSpace(paisId) && paisId.ToUpper() == "GTM")
                dptos = await _context.departamentos
                    .Where(d => d.PaisID == "GTM" && d.Estado)
                    .OrderBy(d => d.Nombre)
                    .Select(d => new SelectListItem { Value = d.DepartamentoID.ToString(), Text = d.Nombre })
                    .ToListAsync();
            ViewBag.Departamentos = new SelectList(dptos, "Value", "Text", deptoId);

            // Municipios (si hay depto)
            var municipios = new List<SelectListItem>();
            if (deptoId.HasValue)
                municipios = await _context.municipios
                    .Where(m => m.DepartamentoID == deptoId && m.Estado)
                    .OrderBy(m => m.Nombre)
                    .Select(m => new SelectListItem { Value = m.MunicipioID.ToString(), Text = m.Nombre })
                    .ToListAsync();
            ViewBag.Municipios = new SelectList(municipios, "Value", "Text", municipioId);
        }

        // ===== API cascada País → Departamentos =====
        [HttpGet]
        public async Task<IActionResult> Departamentos(string paisId)
        {
            if (string.IsNullOrWhiteSpace(paisId))
                return Json(Array.Empty<object>());

            var data = new List<object>();
            if (paisId.Trim().ToUpper() == "GTM")
            {
                data = await _context.departamentos
                    .Where(d => d.PaisID == "GTM" && d.Estado)
                    .OrderBy(d => d.Nombre)
                    .Select(d => new { id = d.DepartamentoID, nombre = d.Nombre })
                    .ToListAsync<object>();
            }
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> Municipios(int deptoId)
        {
            if (deptoId <= 0) return Json(Array.Empty<object>());
            var data = await _context.municipios
                .Where(m => m.DepartamentoID == deptoId && m.Estado)
                .OrderBy(m => m.Nombre)
                .Select(m => new { id = m.MunicipioID, nombre = m.Nombre })
                .ToListAsync<object>();
            return Json(data);
        }

        // ====== CREATE (GET) ======
        public async Task<IActionResult> Create()
        {
            await CargarCombosCreateAsync();

            var vm = new EmpleadoCreateVM
            {
                Empleado = new Empleado
                {
                    EmpleadoID = await NextEmpleadoIdAsync(),
                    Estado = true,
                    FechaContratacion = DateTime.Today
                },
                Persona = new Persona
                {
                    FechaRegistro = DateTime.Now
                }
            };

            return IsAjax ? PartialView(vm) : View(vm);
        }

        // ====== CREATE (POST) ======
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EmpleadoCreateVM vm, IFormFile? Foto)
        {
            // Reglas previas
            ModelState.Remove("Empleado.PersonalID");
            ModelState.Remove("Persona.PersonaID");
            ModelState.Remove("Empleado.Puesto");
            ModelState.Remove("Empleado.Persona");

            if (vm.Persona.FechaRegistro == null) vm.Persona.FechaRegistro = DateTime.Now;
            ModelState.Remove("Persona.FechaRegistro");

            var esGtm = string.Equals(vm.Persona.PaisID, "GTM", StringComparison.OrdinalIgnoreCase);
            if (!esGtm)
            {
                ModelState.Remove("Persona.DepartamentoID");
                ModelState.Remove("Persona.MunicipioID");
                vm.Persona.DepartamentoID = null;
                vm.Persona.MunicipioID = null;
            }

            // Validar foto (si viene)
            if (Foto != null && Foto.Length > 0)
                ValidarFoto(Foto);

            if (!ModelState.IsValid)
            {
                await CargarCombosCreateAsync(
                    vm.Empleado?.PuestoID,
                    vm.Persona?.TipoDocumentoID,
                    vm.Persona?.PaisID,
                    vm.Persona?.DepartamentoID,
                    vm.Persona?.MunicipioID
                );

                if (IsAjax) return PartialView(nameof(Create), vm);
                return View(nameof(Create), vm);
            }

            // Guardado
            var nuevoEmpleadoId = await NextEmpleadoIdAsync();
            var nuevoPersonaId = await NextPersonaIdAsync();

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                // Persona
                vm.Persona.PersonaID = nuevoPersonaId;

                if (Foto != null && Foto.Length > 0)
                    vm.Persona.FotoPath = await GuardarFotoAsync(nuevoPersonaId, Foto);

                _context.personas.Add(vm.Persona);
                await _context.SaveChangesAsync();

                // Empleado
                var empleado = vm.Empleado;
                empleado.EmpleadoID = nuevoEmpleadoId;
                empleado.PersonalID = vm.Persona.PersonaID;
                empleado.FechaContratacion = empleado.FechaContratacion ?? DateTime.Today;

                _context.empleados.Add(empleado);
                await _context.SaveChangesAsync();

                await tx.CommitAsync();

                if (IsAjax)
                    return Ok(new
                    {
                        ok = true,
                        message = "Persona y Empleado creados correctamente.",
                        redirectUrl = Url.Action(nameof(Index), "Empleados")
                    });

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                if (IsAjax)
                    return StatusCode(500, new { ok = false, message = "Error al guardar: " + ex.Message });

                ModelState.AddModelError(string.Empty, "Error al guardar: " + ex.Message);
                await CargarCombosCreateAsync(
                    vm.Empleado?.PuestoID,
                    vm.Persona?.TipoDocumentoID,
                    vm.Persona?.PaisID,
                    vm.Persona?.DepartamentoID,
                    vm.Persona?.MunicipioID
                );
                return View(nameof(Create), vm);
            }
        }

        // ====== EDIT (GET) ======
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var emp = await _context.empleados
                .Include(e => e.Persona)
                .FirstOrDefaultAsync(e => e.EmpleadoID == id);

            if (emp == null) return NotFound();

            await CargarCombosCreateAsync(
                emp.PuestoID,
                emp.Persona.TipoDocumentoID,
                emp.Persona.PaisID,
                emp.Persona.DepartamentoID,
                emp.Persona.MunicipioID
            );

            var vm = new EmpleadoCreateVM
            {
                Empleado = emp,
                Persona = emp.Persona
            };

            return IsAjax ? PartialView(vm) : View(vm);
        }

        // ====== EDIT (POST) ======
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EmpleadoCreateVM vm, IFormFile? Foto, bool QuitarFoto = false)
        {
            ModelState.Remove("Empleado.Puesto");
            ModelState.Remove("Empleado.Persona");

            var esGtm = string.Equals(vm.Persona.PaisID, "GTM", StringComparison.OrdinalIgnoreCase);
            if (!esGtm)
            {
                ModelState.Remove("Persona.DepartamentoID");
                ModelState.Remove("Persona.MunicipioID");
                vm.Persona.DepartamentoID = null;
                vm.Persona.MunicipioID = null;
            }

            if (Foto != null && Foto.Length > 0)
                ValidarFoto(Foto);

            if (!ModelState.IsValid)
            {
                await CargarCombosCreateAsync(
                    vm.Empleado?.PuestoID,
                    vm.Persona?.TipoDocumentoID,
                    vm.Persona?.PaisID,
                    vm.Persona?.DepartamentoID,
                    vm.Persona?.MunicipioID
                );
                if (IsAjax) return PartialView(nameof(Edit), vm);
                return View(nameof(Edit), vm);
            }

            var db = await _context.empleados
                .Include(e => e.Persona)
                .FirstOrDefaultAsync(e => e.EmpleadoID == vm.Empleado.EmpleadoID);
            if (db == null) return NotFound();

            bool hadChanges =
                !string.Equals(db.PuestoID, vm.Empleado.PuestoID, StringComparison.Ordinal) ||
                db.FechaContratacion != vm.Empleado.FechaContratacion ||
                db.Estado != vm.Empleado.Estado ||
                !string.Equals(db.Persona.PrimerNombre, vm.Persona.PrimerNombre, StringComparison.Ordinal) ||
                !string.Equals(db.Persona.SegundoNombre, vm.Persona.SegundoNombre, StringComparison.Ordinal) ||
                !string.Equals(db.Persona.PrimerApellido, vm.Persona.PrimerApellido, StringComparison.Ordinal) ||
                !string.Equals(db.Persona.SegundoApellido, vm.Persona.SegundoApellido, StringComparison.Ordinal) ||
                !string.Equals(db.Persona.ApellidoCasada, vm.Persona.ApellidoCasada, StringComparison.Ordinal) ||
                !string.Equals(db.Persona.Email, vm.Persona.Email, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(db.Persona.Telefono1, vm.Persona.Telefono1, StringComparison.Ordinal) ||
                !string.Equals(db.Persona.Telefono2, vm.Persona.Telefono2, StringComparison.Ordinal) ||
                !string.Equals(db.Persona.Direccion, vm.Persona.Direccion, StringComparison.Ordinal) ||
                !string.Equals(db.Persona.TipoDocumentoID, vm.Persona.TipoDocumentoID, StringComparison.Ordinal) ||
                !string.Equals(db.Persona.NumeroDocumento, vm.Persona.NumeroDocumento, StringComparison.Ordinal) ||
                !string.Equals(db.Persona.Nit, vm.Persona.Nit, StringComparison.Ordinal) ||
                db.Persona.FechaNacimiento != vm.Persona.FechaNacimiento ||
                !string.Equals(db.Persona.PaisID, vm.Persona.PaisID, StringComparison.OrdinalIgnoreCase) ||
                db.Persona.DepartamentoID != vm.Persona.DepartamentoID ||
                db.Persona.MunicipioID != vm.Persona.MunicipioID
                || (Foto != null && Foto.Length > 0)
                || (QuitarFoto && !string.IsNullOrWhiteSpace(db.Persona.FotoPath));

            if (!hadChanges)
            {
                if (IsAjax) return Ok(new { ok = false, reason = "nochanges", message = "Realiza un cambio antes de guardar." });

                TempData["NoChanges"] = true;
                await CargarCombosCreateAsync(db.PuestoID, db.Persona.TipoDocumentoID, db.Persona.PaisID, db.Persona.DepartamentoID, db.Persona.MunicipioID);
                var vmBack = new EmpleadoCreateVM { Empleado = db, Persona = db.Persona };
                return View(nameof(Edit), vmBack);
            }

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                // Persona
                var p = db.Persona;
                p.PrimerNombre = vm.Persona.PrimerNombre;
                p.SegundoNombre = vm.Persona.SegundoNombre;
                p.PrimerApellido = vm.Persona.PrimerApellido;
                p.SegundoApellido = vm.Persona.SegundoApellido;
                p.ApellidoCasada = vm.Persona.ApellidoCasada;
                p.Email = vm.Persona.Email;
                p.Telefono1 = vm.Persona.Telefono1;
                p.Telefono2 = vm.Persona.Telefono2;
                p.Direccion = vm.Persona.Direccion;
                p.TipoDocumentoID = vm.Persona.TipoDocumentoID;
                p.NumeroDocumento = vm.Persona.NumeroDocumento;
                p.Nit = vm.Persona.Nit;
                p.FechaNacimiento = vm.Persona.FechaNacimiento;
                p.PaisID = vm.Persona.PaisID;
                p.DepartamentoID = vm.Persona.DepartamentoID;
                p.MunicipioID = vm.Persona.MunicipioID;

                // Foto
                if (QuitarFoto && !string.IsNullOrWhiteSpace(p.FotoPath))
                {
                    BorrarFotoFisica(p.FotoPath);
                    p.FotoPath = null;
                }
                else if (Foto != null && Foto.Length > 0)
                {
                    var nueva = await GuardarFotoAsync(p.PersonaID, Foto);
                    if (!string.IsNullOrWhiteSpace(p.FotoPath))
                        BorrarFotoFisica(p.FotoPath);
                    p.FotoPath = nueva;
                }
                else
                {
                    p.FotoPath = vm.Persona.FotoPath;
                }

                // Empleado
                db.PuestoID = vm.Empleado.PuestoID;
                db.FechaContratacion = vm.Empleado.FechaContratacion;
                db.Estado = vm.Empleado.Estado;

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return IsAjax
                    ? Ok(new { ok = true, message = "Empleado actualizado correctamente.", redirectUrl = Url.Action(nameof(Index), "Empleados") })
                    : RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();

                if (IsAjax) return StatusCode(500, new { ok = false, message = "Error al actualizar: " + ex.Message });

                ModelState.AddModelError(string.Empty, "Error al actualizar: " + ex.Message);
                await CargarCombosCreateAsync(
                    vm.Empleado?.PuestoID,
                    vm.Persona?.TipoDocumentoID,
                    vm.Persona?.PaisID,
                    vm.Persona?.DepartamentoID,
                    vm.Persona?.MunicipioID
                );
                return View(nameof(Edit), vm);
            }
        }

        // ====== DETAILS ======
        //public async Task<IActionResult> Details(string id)
        //{
        //    var model = await _context.empleados
        //        .AsNoTracking()
        //        .Include(e => e.Persona)
        //        .Include(e => e.Puesto)
        //        .FirstOrDefaultAsync(e => e.EmpleadoID == id);
        //    if (model == null) return NotFound();
        //    return IsAjax ? PartialView(model) : View(model);
        //}

        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            var model = await _context.empleados
                .AsNoTracking()
                .Include(e => e.Persona)
                .Include(e => e.Puesto)
                .FirstOrDefaultAsync(e => e.EmpleadoID == id);
            if (model == null) return NotFound();

            ViewBag.IsPartial = IsAjax; // <- igual que Clientes
            return IsAjax ? PartialView(model) : View(model);
        }


        // ====== DELETE (activar/desactivar) ======
        [HttpGet]
        public async Task<IActionResult> Delete(string id)
        {
            var model = await _context.empleados
                .AsNoTracking()
                .Include(e => e.Persona)
                .Include(e => e.Puesto)
                .FirstOrDefaultAsync(e => e.EmpleadoID == id);
            if (model == null) return NotFound();

            ViewBag.IsPartial = IsAjax; // igual que en Clientes
            return IsAjax ? PartialView(model) : View(model);
        }


        //public async Task<IActionResult> Delete(string id)
        //{
        //    var model = await _context.empleados
        //        .AsNoTracking()
        //        .Include(e => e.Persona)
        //        .Include(e => e.Puesto)
        //        .FirstOrDefaultAsync(e => e.EmpleadoID == id);
        //    if (model == null) return NotFound();
        //    return IsAjax ? PartialView(model) : View(model);
        //}

        // ====== ToggleEstado ======
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleEstado(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();

            var emp = await _context.empleados.FirstOrDefaultAsync(e => e.EmpleadoID == id);
            if (emp == null) return NotFound();

            emp.Estado = !emp.Estado;
            await _context.SaveChangesAsync();

            if (IsAjax)
            {
                var list = await _context.empleados
                    .AsNoTracking()
                    .Include(e => e.Persona)
                    .Include(e => e.Puesto)
                    .OrderBy(e => e.EmpleadoID)
                    .ToListAsync();

                return PartialView(nameof(Index), list);
            }

            return RedirectToAction(nameof(Index));
        }

        // ====== ExportPdf ======
        [HttpGet]
        public async Task<IActionResult> ExportPdf(
            [FromQuery] string? codigo,
            [FromQuery] string? persona,
            [FromQuery] string? puesto,
            [FromQuery] string? estado,
            [FromQuery(Name = "fIni")] string? fIniStr,
            [FromQuery(Name = "fFin")] string? fFinStr)
        {
            var q = _context.empleados
                .AsNoTracking()
                .Include(e => e.Persona)
                .Include(e => e.Puesto)
                .AsQueryable();

            q = FiltrarEmpleadosSqlServer(q, codigo, persona, puesto, estado, fIniStr, fFinStr);

            var model = await q.OrderBy(e => e.EmpleadoID).ToListAsync();

            return new ViewAsPdf("ReportePdf", model)
            {
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,
                CustomSwitches = "--footer-center \"Página [page] de [toPage]\" --footer-font-size 8 --footer-spacing 5"
            };
        }

        // ====== Utilidades de imágenes ======
        private static readonly string[] _extPermitidas = new[] { ".jpg", ".jpeg", ".png", ".webp" };

        private void ValidarFoto(IFormFile foto)
        {
            if (foto == null || foto.Length == 0) return;
            var ext = Path.GetExtension(foto.FileName).ToLowerInvariant();
            if (!_extPermitidas.Contains(ext))
                ModelState.AddModelError("Foto", "Formato no permitido. Usa JPG, PNG o WebP.");
            if (foto.Length > 3 * 1024 * 1024)
                ModelState.AddModelError("Foto", "La imagen no debe superar 3 MB.");
        }

        private async Task<string> GuardarFotoAsync(string personaId, IFormFile foto)
        {
            var ext = Path.GetExtension(foto.FileName).ToLowerInvariant();
            var folderAbs = Path.Combine(_env.WebRootPath, "img", "uploads", "empleados");
            Directory.CreateDirectory(folderAbs);

            var fileName = $"{personaId}_{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
            var absPath = Path.Combine(folderAbs, fileName);

            using (var fs = new FileStream(absPath, FileMode.Create))
                await foto.CopyToAsync(fs);

            var rel = Path.Combine("img", "uploads", "empleados", fileName).Replace("\\", "/");
            return rel;
        }

        private void BorrarFotoFisica(string? relPath)
        {
            if (string.IsNullOrWhiteSpace(relPath)) return;
            try
            {
                var abs = Path.Combine(_env.WebRootPath, relPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
                if (System.IO.File.Exists(abs))
                    System.IO.File.Delete(abs);
            }
            catch { /* ignora errores de IO */ }
        }

        [HttpGet]
        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Client)]
        public async Task<IActionResult> Avatar(string id)
        {
            var data = await _context.empleados
                .AsNoTracking()
                .Include(e => e.Persona)
                .Where(e => e.EmpleadoID == id)
                .Select(e => new { e.EmpleadoID, e.Persona.FotoPath })
                .FirstOrDefaultAsync();

            string defaultPath = Path.Combine(_env.WebRootPath, "img", "DefaultUsuario.png");
            string filePath = defaultPath;

            if (data?.FotoPath is string rel && !string.IsNullOrWhiteSpace(rel))
            {
                var safeRel = rel.TrimStart('/', '\\');
                var abs = Path.Combine(_env.WebRootPath, safeRel);
                if (System.IO.File.Exists(abs)) filePath = abs;
            }

            var contentType = Path.GetExtension(filePath).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };

            return PhysicalFile(filePath, contentType);
        }

    }
}
