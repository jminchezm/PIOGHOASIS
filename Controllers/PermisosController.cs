// Controllers/PermisosController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PIOGHOASIS.Infraestructure.Data;
using PIOGHOASIS.Infraestructure.Security;
using PIOGHOASIS.Models;
using PIOGHOASIS.Models.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PIOGHOASIS.Controllers
{
    //[RequireModule("PERMISOS")]
    public class PermisosController : Controller
    {
        private readonly AppDbContext _ctx;
        public PermisosController(AppDbContext ctx) => _ctx = ctx;

        private bool IsAjax => Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        // ===== INDEX =====
        public async Task<IActionResult> Index()
        {
            var rows = await _ctx.roles
                .Select(r => new RolPermisosRowVM
                {
                    RolID = r.RolID,
                    RolNombre = r.Nombre,
                    ModulosPermitidos = _ctx.rolModuloPermisos
                        .Where(p => p.RolID == r.RolID && p.PuedeAcceder)
                        .Join(_ctx.modulos, p => p.ModuloID, m => m.ModuloID, (p, m) => m.Nombre)
                        .OrderBy(n => n)
                        .ToList()
                })
                .OrderBy(r => r.RolNombre)
                .ToListAsync();

            return IsAjax ? PartialView(rows) : View(rows);
        }

        // ===== helper para armar el VM de create/edit =====
        private async Task<PermisosRolVM> BuildVmAsync(string? rolId = null)
        {
            var roles = await _ctx.roles
                .OrderBy(r => r.Nombre)
                .Select(r => new SelectListItem { Value = r.RolID, Text = r.Nombre })
                .ToListAsync();

            var mods = await _ctx.modulos
                .OrderBy(m => m.Nombre)
                .Select(m => new { m.ModuloID, m.Codigo, m.Nombre })
                .ToListAsync();

            var vm = new PermisosRolVM
            {
                RolID = rolId ?? "",
                Roles = roles,
                Items = mods.Select(m => new PermisoModuloItemVM
                {
                    ModuloID = m.ModuloID,
                    Codigo = m.Codigo,
                    Nombre = m.Nombre
                }).ToList()
            };

            if (!string.IsNullOrWhiteSpace(rolId))
            {
                var actuales = await _ctx.rolModuloPermisos
                    .Where(p => p.RolID == rolId)
                    .ToDictionaryAsync(p => p.ModuloID, p => p.PuedeAcceder);

                foreach (var it in vm.Items)
                    it.PuedeAcceder = actuales.TryGetValue(it.ModuloID, out var acc) && acc;

                vm.RolNombre = roles.FirstOrDefault(x => x.Value == rolId)?.Text;
            }

            return vm;
        }

        // ===== CREATE =====
        public async Task<IActionResult> Create() =>
            IsAjax ? PartialView(await BuildVmAsync()) : View(await BuildVmAsync());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PermisosRolVM vm)
        {
            if (string.IsNullOrWhiteSpace(vm.RolID))
            {
                ModelState.AddModelError(nameof(vm.RolID), "Seleccione un rol.");
                return IsAjax ? PartialView(await BuildVmAsync()) : View(await BuildVmAsync());
            }

            // Limpia permisos previos del rol (si los hubiera) y guarda los nuevos
            var prev = await _ctx.rolModuloPermisos.Where(p => p.RolID == vm.RolID).ToListAsync();
            if (prev.Count > 0) _ctx.rolModuloPermisos.RemoveRange(prev);

            var now = vm.Items
                .Where(i => i.PuedeAcceder)
                .Select(i => new RolModuloPermiso
                {
                    RolID = vm.RolID,
                    ModuloID = i.ModuloID,
                    PuedeAcceder = true,
                    Estado = true,
                    FechaRegistro = System.DateTime.Now
                }).ToList();

            if (now.Count > 0) await _ctx.rolModuloPermisos.AddRangeAsync(now);
            await _ctx.SaveChangesAsync();

            if (IsAjax) return Json(new { ok = true, redirectUrl = Url.Action(nameof(Index)) });
            return RedirectToAction(nameof(Index));
        }

        // ===== EDIT =====
        public async Task<IActionResult> Edit(string id)
        {
            var vm = await BuildVmAsync(id);
            if (string.IsNullOrWhiteSpace(vm.RolID)) return NotFound();
            return IsAjax ? PartialView(vm) : View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(PermisosRolVM vm)
        {
            if (string.IsNullOrWhiteSpace(vm.RolID))
            {
                ModelState.AddModelError(nameof(vm.RolID), "Rol inválido.");
                var back = await BuildVmAsync(vm.RolID);
                return IsAjax ? PartialView(back) : View(back);
            }

            // --- Conjunto actual en BD (solo los que pueden acceder) ---
            var current = await _ctx.rolModuloPermisos
                .Where(p => p.RolID == vm.RolID && p.PuedeAcceder)
                .Select(p => p.ModuloID)
                .OrderBy(x => x)
                .ToListAsync();

            // --- Conjunto que viene del formulario ---
            var posted = vm.Items
                .Where(i => i.PuedeAcceder)
                .Select(i => i.ModuloID)
                .OrderBy(x => x)
                .ToList();

            // --- ¿No hubo cambios? ---
            bool noChanges = current.Count == posted.Count && current.SequenceEqual(posted);
            if (noChanges)
            {
                if (IsAjax)
                    return Ok(new { ok = false, reason = "nochanges", message = "Realiza un cambio antes de guardar." });

                TempData["NoChanges"] = true; // por si lo usas fuera del dashboard
                var back = await BuildVmAsync(vm.RolID);
                return View(back);
            }

            // --- Hubo cambios: reemplazar permisos del rol ---
            var prev = await _ctx.rolModuloPermisos.Where(p => p.RolID == vm.RolID).ToListAsync();
            if (prev.Count > 0) _ctx.rolModuloPermisos.RemoveRange(prev);

            var now = posted.Select(mid => new RolModuloPermiso
            {
                RolID = vm.RolID,
                ModuloID = mid,
                PuedeAcceder = true,
                Estado = true,
                FechaRegistro = DateTime.Now
            }).ToList();

            if (now.Count > 0) await _ctx.rolModuloPermisos.AddRangeAsync(now);
            await _ctx.SaveChangesAsync();

            if (IsAjax) return Json(new { ok = true, redirectUrl = Url.Action(nameof(Index)) });
            return RedirectToAction(nameof(Index));
        }
    }
}
