using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PIOGHOASIS.Infraestructure.Data;
using PIOGHOASIS.Models;
using PIOGHOASIS.Models.Entities;
using PIOGHOASIS.Models.ViewModels;

namespace PIOGHOASIS.Controllers
{
    [Authorize]
    public class ClientesController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        private const string SqlCollation = "SQL_Latin1_General_CP1_CI_AI";

        public ClientesController(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        // === Utilidades generales ===
        private bool IsAjax =>
            string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

        private IQueryable<Cliente> BaseQuery() =>
            _db.clientes.AsNoTracking().Include(c => c.Persona);

        private object ModelStateErrors() =>
            ModelState.Where(x => x.Value!.Errors.Count > 0)
                      .ToDictionary(x => x.Key, x => x.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

        private async Task<string> NextClienteIdAsync()
        {
            var ids = await _db.clientes.AsNoTracking()
                        .Where(x => x.ClienteID.StartsWith("CLI"))
                        .Select(x => x.ClienteID)
                        .ToListAsync();

            int max = 0;
            foreach (var id in ids)
            {
                var digits = new string(id.SkipWhile(c => !char.IsDigit(c)).ToArray());
                if (int.TryParse(digits, out var n) && n > max) max = n;
            }

            string next;
            do { max++; next = $"CLI{max:0000000}"; }
            while (await _db.clientes.AnyAsync(c => c.ClienteID == next));

            return next;
        }

        private async Task<string> NextPersonaIdAsync()
        {
            var ids = await _db.personas.AsNoTracking()
                        .Where(x => x.PersonaID.StartsWith("PER"))
                        .Select(x => x.PersonaID)
                        .ToListAsync();

            int max = 0;
            foreach (var id in ids)
            {
                var digits = new string(id.SkipWhile(c => !char.IsDigit(c)).ToArray());
                if (int.TryParse(digits, out var n) && n > max) max = n;
            }

            string next;
            do { max++; next = $"PER{max:0000000}"; }
            while (await _db.personas.AnyAsync(p => p.PersonaID == next));

            return next;
        }

        private static string NombreCompleto(Persona? p)
        {
            if (p == null) return "—";
            var partes = new[] { p.PrimerNombre, p.SegundoNombre, p.PrimerApellido, p.SegundoApellido }
                         .Where(s => !string.IsNullOrWhiteSpace(s));
            return string.Join(" ", partes);
        }

        private IQueryable<Cliente> Filtrar(IQueryable<Cliente> q, string? codigo, string? nombre, string? estado)
        {
            if (!string.IsNullOrWhiteSpace(codigo))
                q = q.Where(c => c.ClienteID.Contains(codigo));

            if (!string.IsNullOrWhiteSpace(nombre))
            {
                var term = nombre.Trim();
                q = q.Where(c =>
                    EF.Functions.Like(
                        EF.Functions.Collate(
                            ((c.Persona.PrimerNombre ?? "") + " " +
                             (c.Persona.SegundoNombre ?? "") + " " +
                             (c.Persona.PrimerApellido ?? "") + " " +
                             (c.Persona.SegundoApellido ?? "")),
                        SqlCollation), $"%{term}%")
                    || EF.Functions.Like(EF.Functions.Collate(c.Persona.NumeroDocumento ?? "", SqlCollation), $"%{term}%")
                );
            }

            if (!string.IsNullOrWhiteSpace(estado))
            {
                if (estado == "1") q = q.Where(c => c.Estado);
                else if (estado == "0") q = q.Where(c => !c.Estado);
            }

            return q;
        }

        // === Guardado de foto en wwwroot/img/uploads/clientes ===
        private async Task<string?> SaveClienteFotoAsync(string clienteId, IFormFile? file)
        {
            if (file == null || file.Length == 0) return null;

            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";

            var relDir = Path.Combine("img", "uploads", "clientes");
            var absDir = Path.Combine(_env.WebRootPath, relDir);
            Directory.CreateDirectory(absDir);

            var fileName = $"{clienteId}_{DateTime.UtcNow:yyyyMMddHHmmssfff}{ext}".Replace(" ", "");
            var absPath = Path.Combine(absDir, fileName);

            using (var fs = new FileStream(absPath, FileMode.Create))
                await file.CopyToAsync(fs);

            return Path.Combine(relDir, fileName).Replace("\\", "/");
        }

        private void BorrarFotoFisicaCliente(string? relPath)
        {
            if (string.IsNullOrWhiteSpace(relPath)) return;
            try
            {
                var abs = Path.Combine(_env.WebRootPath, relPath.Replace("/", Path.DirectorySeparatorChar.ToString()));
                if (System.IO.File.Exists(abs))
                    System.IO.File.Delete(abs);
            }
            catch { /* ignore IO errors */ }
        }

        // === LISTADO ===
        [HttpGet]
        public async Task<IActionResult> Index(string? codigo, string? nombre, string? estado)
        {
            if (!Request.Query.ContainsKey("estado")) estado = "1";

            var q = Filtrar(BaseQuery(), codigo, nombre, estado);
            var model = await q.OrderBy(c => c.ClienteID).ToListAsync();

            ViewBag.IsPartial = IsAjax;
            ViewBag.Codigo = codigo; ViewBag.Nombre = nombre; ViewBag.Estado = estado;

            return View(model);
        }

        // === AVATAR ===
        [HttpGet]
        [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Client)]
        public async Task<IActionResult> Avatar(string id)
        {
            var data = await _db.clientes
                .AsNoTracking()
                .Include(c => c.Persona)
                .Where(c => c.ClienteID == id)
                .Select(c => new { c.ClienteID, c.Persona.FotoPath })
                .FirstOrDefaultAsync();

            string filePath;
            if (data?.FotoPath is string rel && !string.IsNullOrWhiteSpace(rel))
            {
                var safeRel = rel.TrimStart('/', '\\');
                filePath = Path.Combine(_env.WebRootPath, safeRel);
                if (!System.IO.File.Exists(filePath))
                    filePath = Path.Combine(_env.WebRootPath, "img", "DefaultUsuario.png");
            }
            else
            {
                filePath = Path.Combine(_env.WebRootPath, "img", "DefaultUsuario.png");
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

        // === LLENADO DE COMBOS (Create/Edit) ===
        private async Task CargarCombosCreateAsync(string? tipoDocumentoId = null, string? paisId = null, int? deptoId = null, int? municipioId = null)
        {
            var tdocs = await _db.tipoDocumentos
                .Where(t => t.Estado)
                .OrderBy(t => t.TipoDocumentoID)
                .Select(t => new SelectListItem { Value = t.TipoDocumentoID, Text = t.TipoDocumentoID + " - " + t.Nombre })
                .ToListAsync();
            ViewBag.TiposDocumento = new SelectList(tdocs, "Value", "Text", tipoDocumentoId);

            var paises = await _db.paises
                .Where(p => p.Estado)
                .OrderBy(p => p.PaisID)
                .Select(p => new SelectListItem { Value = p.PaisID, Text = p.PaisID + " - " + p.Nombre })
                .ToListAsync();
            ViewBag.Paises = new SelectList(paises, "Value", "Text", paisId);

            // Departamentos solo cuando País = GTM (si no, lista vacía pero controles siguen habilitados)
            var dptos = new List<SelectListItem>();
            if (!string.IsNullOrWhiteSpace(paisId) && paisId.ToUpper() == "GTM")
                dptos = await _db.departamentos
                    .Where(d => d.PaisID == "GTM" && d.Estado)
                    .OrderBy(d => d.Nombre)
                    .Select(d => new SelectListItem { Value = d.DepartamentoID.ToString(), Text = d.Nombre })
                    .ToListAsync();
            ViewBag.Departamentos = new SelectList(dptos, "Value", "Text", deptoId);

            var municipios = new List<SelectListItem>();
            if (deptoId.HasValue)
                municipios = await _db.municipios
                    .Where(m => m.DepartamentoID == deptoId && m.Estado)
                    .OrderBy(m => m.Nombre)
                    .Select(m => new SelectListItem { Value = m.MunicipioID.ToString(), Text = m.Nombre })
                    .ToListAsync();
            ViewBag.Municipios = new SelectList(municipios, "Value", "Text", municipioId);
        }

        // === ENDPOINTS DE CASCADA ===
        [HttpGet]
        public async Task<IActionResult> Departamentos(string paisId)
        {
            if (string.IsNullOrWhiteSpace(paisId)) return Json(Array.Empty<object>());
            var data = new List<object>();
            if (paisId.Trim().ToUpper() == "GTM")
                data = await _db.departamentos
                    .Where(d => d.PaisID == "GTM" && d.Estado)
                    .OrderBy(d => d.Nombre)
                    .Select(d => new { id = d.DepartamentoID, nombre = d.Nombre })
                    .ToListAsync<object>();
            return Json(data);
        }

        [HttpGet]
        public async Task<IActionResult> Municipios(int deptoId)
        {
            if (deptoId <= 0) return Json(Array.Empty<object>());
            var data = await _db.municipios
                .Where(m => m.DepartamentoID == deptoId && m.Estado)
                .OrderBy(m => m.Nombre)
                .Select(m => new { id = m.MunicipioID, nombre = m.Nombre })
                .ToListAsync<object>();
            return Json(data);
        }

        // === CREATE ===
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await CargarCombosCreateAsync();

            var vm = new ClienteFormVm
            {
                Cliente = new Cliente { ClienteID = await NextClienteIdAsync(), Estado = true },
                Persona = new Persona { PersonaID = await NextPersonaIdAsync(), FechaRegistro = DateTime.Now }
            };
            ViewBag.IsPartial = IsAjax;
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ClienteFormVm vm)
        {
            ModelState.Remove("Cliente.Persona");
            ModelState.Remove("Persona.Empleado");
            ModelState.Remove("Persona.PersonaID");
            ModelState.Remove("Cliente.PersonaID");

            if (vm.Persona.FechaRegistro == null)
                vm.Persona.FechaRegistro = DateTime.Now;
            ModelState.Remove("Persona.FechaRegistro");

            // Departamento / Municipio SIEMPRE opcionales
            ModelState.Remove("Persona.DepartamentoID");
            ModelState.Remove("Persona.MunicipioID");

            vm.Persona.TipoDocumentoID = vm.Persona.TipoDocumentoID?.Trim();
            if (string.IsNullOrEmpty(vm.Persona.TipoDocumentoID))
                ModelState.AddModelError("Persona.TipoDocumentoID", "El campo Tipo Documento es obligatorio.");

            if (!ModelState.IsValid)
            {
                if (IsAjax) return BadRequest(new { ok = false, errors = ModelStateErrors() });

                await CargarCombosCreateAsync(vm.Persona?.TipoDocumentoID, vm.Persona?.PaisID, vm.Persona?.DepartamentoID, vm.Persona?.MunicipioID);
                return View(vm);
            }

            vm.Cliente.ClienteID = await NextClienteIdAsync();
            if (string.IsNullOrWhiteSpace(vm.Persona.PersonaID))
                vm.Persona.PersonaID = await NextPersonaIdAsync();

            var rel = await SaveClienteFotoAsync(vm.Cliente.ClienteID, vm.Foto);
            if (!string.IsNullOrWhiteSpace(rel)) vm.Persona.FotoPath = rel;

            try
            {
                _db.personas.Add(vm.Persona);
                vm.Cliente.PersonaID = vm.Persona.PersonaID;
                _db.clientes.Add(vm.Cliente);
                await _db.SaveChangesAsync();

                return IsAjax
                    ? Ok(new { ok = true, redirectUrl = Url.Action(nameof(Index)) })
                    : RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                if (IsAjax) return StatusCode(500, new { ok = false, message = "Error al guardar: " + ex.Message });

                ModelState.AddModelError(string.Empty, "Error al guardar: " + ex.Message);
                await CargarCombosCreateAsync(vm.Persona?.TipoDocumentoID, vm.Persona?.PaisID, vm.Persona?.DepartamentoID, vm.Persona?.MunicipioID);
                return View(vm);
            }
        }

        // === EDIT ===
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var c = await _db.clientes
                .Include(x => x.Persona)
                .FirstOrDefaultAsync(x => x.ClienteID == id);

            if (c == null) return NotFound();

            await CargarCombosCreateAsync(
                c.Persona?.TipoDocumentoID,
                c.Persona?.PaisID,
                c.Persona?.DepartamentoID,
                c.Persona?.MunicipioID
            );

            var vm = new ClienteFormVm { Cliente = c, Persona = c.Persona };
            ViewBag.IsPartial = IsAjax;
            return View(vm);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, ClienteFormVm vm, IFormFile? Foto, bool QuitarFoto = false)
        {
            if (id != vm?.Cliente?.ClienteID)
                return BadRequest(new { ok = false, message = "El id de la ruta no coincide con el del formulario." });

            // Navegaciones que no vienen en el post
            ModelState.Remove("Cliente.Persona");
            ModelState.Remove("Persona.Empleado");
            ModelState.Remove("Cliente.PersonaID");

            // ⚠️ Igual que en Create: estos dos los tratamos como opcionales SIEMPRE
            ModelState.Remove("Persona.DepartamentoID");
            ModelState.Remove("Persona.MunicipioID");

            // ⚠️ Suele causar 400 si queda vacío o no viaja
            ModelState.Remove("Persona.FechaRegistro");
            // Si en tu modelo es [Required] y no viaja, conserva el valor actual
            // (lo hacemos abajo al mapear con la entidad de BD)

            // Foto (valida tamaño/extension)
            if (Foto is { Length: > 0 }) ValidarFotoCliente(Foto);

            if (!ModelState.IsValid)
            {
                // 👇 Devuelve 400 con el diccionario de errores (tu JS ya lo muestra por campo)
                return BadRequest(new { ok = false, errors = ModelStateErrors() });
            }

            var db = await _db.clientes
                .Include(c => c.Persona)
                .FirstOrDefaultAsync(c => c.ClienteID == vm.Cliente.ClienteID);

            if (db == null) return NotFound();

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                // Persona
                var p = db.Persona;
                p.PrimerNombre = vm.Persona.PrimerNombre;
                p.SegundoNombre = vm.Persona.SegundoNombre;
                p.PrimerApellido = vm.Persona.PrimerApellido;
                p.SegundoApellido = vm.Persona.SegundoApellido;
                p.Email = vm.Persona.Email;
                p.Telefono1 = vm.Persona.Telefono1;
                p.Telefono2 = vm.Persona.Telefono2;
                p.Direccion = vm.Persona.Direccion;
                p.TipoDocumentoID = vm.Persona.TipoDocumentoID;
                p.NumeroDocumento = vm.Persona.NumeroDocumento;
                p.Nit = vm.Persona.Nit;
                p.FechaNacimiento = vm.Persona.FechaNacimiento;
                p.PaisID = vm.Persona.PaisID;
                p.DepartamentoID = vm.Persona.DepartamentoID;   // pueden venir null
                p.MunicipioID = vm.Persona.MunicipioID;

                // Si por validación cliente no viajó FechaRegistro, conserva la que está en BD
                if (vm.Persona.FechaRegistro == null)
                    vm.Persona.FechaRegistro = p.FechaRegistro;

                // Foto
                if (QuitarFoto && !string.IsNullOrWhiteSpace(p.FotoPath))
                {
                    BorrarFotoFisicaCliente(p.FotoPath);
                    p.FotoPath = null;
                }
                else if (Foto is { Length: > 0 })
                {
                    var nueva = await SaveClienteFotoAsync(db.ClienteID, Foto);
                    if (!string.IsNullOrWhiteSpace(p.FotoPath))
                        BorrarFotoFisicaCliente(p.FotoPath);
                    p.FotoPath = nueva;
                }
                else
                {
                    // Conservar la que venía en el hidden (si la agregas en la vista)
                    p.FotoPath = vm.Persona.FotoPath;
                }

                // Cliente
                db.Estado = vm.Cliente.Estado;

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                return IsAjax
                    ? Ok(new { ok = true, message = "Cliente actualizado correctamente.", redirectUrl = Url.Action(nameof(Index)) })
                    : RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return IsAjax
                    ? StatusCode(500, new { ok = false, message = "Error al actualizar: " + ex.Message })
                    : Problem("Error al actualizar: " + ex.Message);
            }
        }





        // === Validación de foto ===
        private static readonly string[] _extPermitidasCli = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        private void ValidarFotoCliente(IFormFile foto)
        {
            if (foto == null || foto.Length == 0) return;
            var ext = Path.GetExtension(foto.FileName).ToLowerInvariant();
            if (!_extPermitidasCli.Contains(ext))
                ModelState.AddModelError("Foto", "Formato no permitido. Usa JPG, PNG o WebP.");
            if (foto.Length > 3 * 1024 * 1024)
                ModelState.AddModelError("Foto", "La imagen no debe superar 3 MB.");
        }

        // === DETAILS ===
        [HttpGet]
        public async Task<IActionResult> Details(string id)
        {
            var c = await BaseQuery().FirstOrDefaultAsync(x => x.ClienteID == id);
            if (c == null) return NotFound();
            ViewBag.IsPartial = IsAjax;
            return View(c);
        }

        // === DELETE (toggle estado) ===
        [HttpGet]
        public async Task<IActionResult> Delete(string id)
        {
            var c = await BaseQuery().FirstOrDefaultAsync(x => x.ClienteID == id);
            if (c == null) return NotFound();
            ViewBag.IsPartial = IsAjax;
            return View(c);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleEstado(string id)
        {
            var c = await _db.clientes.FirstOrDefaultAsync(x => x.ClienteID == id);
            if (c == null) return NotFound();

            c.Estado = !c.Estado;
            await _db.SaveChangesAsync();

            if (IsAjax)
            {
                var list = await BaseQuery().OrderBy(x => x.ClienteID).ToListAsync();
                return PartialView(nameof(Index), list);
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
