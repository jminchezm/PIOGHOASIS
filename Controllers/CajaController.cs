// Controllers/CajaController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PIOGHOASIS.Infraestructure.Data;
using PIOGHOASIS.Models;
using PIOGHOASIS.Models.Entities;
using PIOGHOASIS.Models.ViewModels;
using Rotativa.AspNetCore;
using Rotativa.AspNetCore.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace PIOGHOASIS.Controllers
{
    [Route("Caja")]
    public class CajaController : Controller
    {
        private readonly AppDbContext _db;
        public CajaController(AppDbContext db) { _db = db; }

        private bool IsAjax => Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        // Helpers
        private async Task<string> NextCodigoAsync()
        {
            // CAJ000001, CAJ000002...
            var cods = await _db.cajas.Select(c => c.Codigo).ToListAsync();
            int max = 0;
            foreach (var c in cods)
            {
                var digits = new string((c ?? "").Where(char.IsDigit).ToArray());
                if (int.TryParse(digits, out var n) && n > max) max = n;
            }
            return $"CAJ{(max + 1).ToString("D7")}";
        }

        // INDEX
        [HttpGet("Index")]
        public async Task<IActionResult> Index([FromQuery] CajaIndexFiltrosVM f)
        {
            var q = _db.cajas
                .Include(c => c.EstadoCaja)
                .Where(c=> c.UsuarioAperturaID.Contains(GetUserId()))
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(f.Codigo))
                q = q.Where(c => c.Codigo.Contains(f.Codigo));

            if (!string.IsNullOrWhiteSpace(f.Usuario))
                q = q.Where(c => c.UsuarioAperturaID.Contains(f.Usuario));

            if (f.EstadoCajaID.HasValue && f.EstadoCajaID.Value > 0)
                q = q.Where(c => c.EstadoCajaID == f.EstadoCajaID.Value);

            // 🔽 Filtro por rango de fechas (FechaApertura)
            if (f.FechaDesde.HasValue)
            {
                var desde = f.FechaDesde.Value.Date;
                q = q.Where(c => c.FechaApertura >= desde);
            }
            if (f.FechaHasta.HasValue)
            {
                var hasta = f.FechaHasta.Value.Date.AddDays(1).AddTicks(-1);
                q = q.Where(c => c.FechaApertura <= hasta);
            }

            var items = await q
                .OrderByDescending(c => c.CajaID)
                .Select(c => new CajaIndexItemVM
                {
                    CajaID = c.CajaID,
                    Codigo = c.Codigo,
                    Usuario = c.UsuarioAperturaID,
                    FechaApertura = c.FechaApertura,
                    MontoApertura = c.MontoApertura,
                    FechaCierre = c.FechaCierre,
                    Estado = c.EstadoCaja!.Nombre
                })
                .ToListAsync();

            var vm = new CajaIndexVM
            {
                Filtros = f,
                Items = items,
                Estados = await _db.estadosCaja.AsNoTracking().Where(e => e.Estado).ToListAsync()
            };

            return View(vm);
        }
        //[HttpGet("Index")]
        //public async Task<IActionResult> Index([FromQuery] CajaIndexFiltrosVM f)
        //{
        //    var q = _db.cajas
        //        .Include(c => c.EstadoCaja)
        //        .AsNoTracking()
        //        .AsQueryable();

        //    if (!string.IsNullOrWhiteSpace(f.Codigo))
        //        q = q.Where(c => c.Codigo.Contains(f.Codigo));

        //    if (!string.IsNullOrWhiteSpace(f.Usuario))
        //        q = q.Where(c => c.UsuarioAperturaID.Contains(f.Usuario));

        //    if (f.EstadoCajaID.HasValue && f.EstadoCajaID.Value > 0)
        //        q = q.Where(c => c.EstadoCajaID == f.EstadoCajaID.Value);

        //    var items = await q
        //        .OrderByDescending(c => c.CajaID)
        //        .Select(c => new CajaIndexItemVM
        //        {
        //            CajaID = c.CajaID,
        //            Codigo = c.Codigo,
        //            Usuario = c.UsuarioAperturaID,
        //            FechaApertura = c.FechaApertura,
        //            MontoApertura = c.MontoApertura,
        //            FechaCierre = c.FechaCierre,
        //            Estado = c.EstadoCaja!.Nombre
        //        })
        //        .ToListAsync();

        //    var vm = new CajaIndexVM
        //    {
        //        Filtros = f,
        //        Items = items,
        //        Estados = await _db.estadosCaja.AsNoTracking().Where(e => e.Estado).ToListAsync()
        //    };

        //    return View(vm);
        //}

        // NUEVA CAJA (modal)
        [HttpGet("Nueva")]
        public async Task<IActionResult> Nueva()
        {
            var abierta = await _db.cajas.AnyAsync(c => c.EstadoCajaID == 1 && c.UsuarioAperturaID == GetUserId());
            if (abierta)
                return BadRequest("Ya existe una caja abierta. Cierra la caja actual antes de abrir otra.");

            var usuarioId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";  // <-- ID
            var usuarioNombre = User?.Identity?.Name ?? "usuario";                 // <-- nombre/alias

            var vm = new NuevaCajaVM
            {
                //Codigo = await NextCodigoAsync(),
                Fecha = DateTime.Now,
                UsuarioId = usuarioId,
                Usuario = usuarioNombre
            };
            return PartialView("_NuevaCaja", vm);
        }

        //[HttpGet("Nueva")]
        //public async Task<IActionResult> Nueva()
        //{
        //    var abierta = await _db.cajas.AnyAsync(c => c.EstadoCajaID == 1);
        //    if (abierta)
        //        return BadRequest("Ya existe una caja abierta. Cierra la caja actual antes de abrir otra.");

        //    var vm = new NuevaCajaVM
        //    {
        //        Codigo = await NextCodigoAsync(),
        //        Fecha = DateTime.Now,
        //        Usuario = User?.Identity?.Name ?? "usuario"
        //    };
        //    return PartialView("_NuevaCaja", vm);
        //}

        // ABRIR
        // ABRIR
        [HttpPost("Abrir")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Abrir(NuevaCajaVM vm)
        {
            // 1 caja abierta por usuario
            if (await _db.cajas.AnyAsync(c => c.EstadoCajaID == 1 && c.UsuarioAperturaID == GetUserId()))
                return BadRequest("Ya existe una caja abierta.");

            var caja = new Caja
            {
                // Codigo: NO LO SETEES → lo genera la BD por DEFAULT
                FechaApertura = vm.Fecha,
                UsuarioAperturaID = vm.UsuarioId,
                MontoApertura = vm.MontoApertura,
                EstadoCajaID = 1
            };

            _db.cajas.Add(caja);
            await _db.SaveChangesAsync(); // aquí ya viene Codigo desde la BD

            return Json(new { ok = true, redirectUrl = Url.Action("Index", "Caja") });
        }


        //[HttpPost("Abrir")]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Abrir(NuevaCajaVM vm)
        //{
        //    if (await _db.cajas.AnyAsync(c => c.EstadoCajaID == 1 && c.UsuarioAperturaID.Contains(GetUserId())))
        //        return BadRequest("Ya existe una caja abierta.");

        //    var caja = new Caja
        //    {
        //        Codigo = vm.Codigo,
        //        FechaApertura = vm.Fecha,
        //        UsuarioAperturaID = vm.UsuarioId, // <-- usa el ID
        //        MontoApertura = vm.MontoApertura,
        //        EstadoCajaID = 1 // Abierta
        //    };

        //    _db.cajas.Add(caja);
        //    await _db.SaveChangesAsync();
        //    //return Json(new { ok = true });
        //    return Json(new { ok = true, redirectUrl = Url.Action("Index", "Caja") });
        //    //return IsAjax ? PartialView(nameof(Index), model) : View(model);
        //}


        //[HttpPost("Abrir")]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Abrir(NuevaCajaVM vm)
        //{
        //    if (await _db.cajas.AnyAsync(c => c.EstadoCajaID == 1))
        //        return BadRequest("Ya existe una caja abierta.");

        //    var caja = new Caja
        //    {
        //        Codigo = vm.Codigo,
        //        FechaApertura = vm.Fecha,
        //        UsuarioAperturaID = vm.Usuario,
        //        MontoApertura = vm.MontoApertura,
        //        EstadoCajaID = 1 // Abierta
        //    };
        //    _db.cajas.Add(caja);
        //    await _db.SaveChangesAsync();
        //    return Json(new { ok = true });
        //}

        // DETALLE (modal)
        [HttpGet("Detalle/{id:int}")]
        public async Task<IActionResult> Detalle(int id)
        {
            var caja = await _db.cajas.AsNoTracking()
                            .FirstOrDefaultAsync(c => c.CajaID == id);
            if (caja == null) return NotFound();

            var pagoIds = await _db.cajaPagos
                .Where(p => p.CajaID == id)
                .OrderBy(p => p.CajaPagoID)
                .Select(p => p.PagoReservaID)
                .ToListAsync();

            var pagos = await _db.pagosReserva
            .Where(p => pagoIds.Contains(p.PagoReservaID))
            .Select(p => new {
                p.PagoReservaID,
                p.FechaPago,
                p.MontoPagado,
                p.TipoPagoID,
                ReservaId = (int?)p.ReservaID,                 // <--- ajusta si el FK se llama diferente
                ReservaCodigo = p.Reserva != null
                    ? p.Reserva.Codigo                         // <--- o p.Reserva.Numero / lo que tengas
                    : null,
                Nombre = (p.Reserva != null
                    ? (p.Reserva.Cliente.Persona.PrimerNombre + " " + p.Reserva.Cliente.Persona.PrimerApellido).Trim()
                    : null)
            })
            .OrderBy(p => p.FechaPago)
            .ToListAsync();

            //var pagos = await _db.pagosReserva
            //    .Where(p => pagoIds.Contains(p.PagoReservaID))
            //    .Select(p => new {
            //        p.PagoReservaID,
            //        p.FechaPago,
            //        p.MontoPagado,
            //        p.TipoPagoID,
            //        Nombre = (p.Reserva != null
            //            ? (p.Reserva.Cliente.Persona.PrimerNombre + " " + p.Reserva.Cliente.Persona.PrimerApellido).Trim()
            //            : null)
            //    })
            //    .OrderBy(p => p.FechaPago)
            //    .ToListAsync();

            var resumen = new ResumenMedioVM();
            foreach (var p in pagos)
            {
                switch ((MedioPago)p.TipoPagoID)
                {
                    case MedioPago.Efectivo: resumen.Efectivo += p.MontoPagado; break;
                    case MedioPago.Transferencia: resumen.Transferencia += p.MontoPagado; break;
                    case MedioPago.Deposito: resumen.Deposito += p.MontoPagado; break;
                    case MedioPago.CompraClick: resumen.CompraClick += p.MontoPagado; break;
                    case MedioPago.PlataformaReservas: resumen.PlataformaReservas += p.MontoPagado; break;
                }
            }

            // === AJUSTES ===
            var ajustesDb = await _db.cajaAjustes
                .Where(a => a.CajaID == id)
                .OrderBy(a => a.Fecha)
                .ToListAsync();

            resumen.AjustesIngreso = ajustesDb.Where(a => a.Tipo == 1).Sum(a => a.Monto);
            resumen.AjustesEgreso = ajustesDb.Where(a => a.Tipo == 2).Sum(a => a.Monto);

            var vm = new DetalleCajaVM
            {
                CajaID = caja.CajaID,
                Codigo = caja.Codigo,
                FechaApertura = caja.FechaApertura,
                FechaCierre = caja.FechaCierre,
                UsuarioAperturaID = caja.UsuarioAperturaID,
                UsuarioCierreID = caja.UsuarioCierreID,
                MontoApertura = caja.MontoApertura,
                Resumen = resumen,
                Pagos = pagos.Select((p, i) => new PagoLineaVM
                {
                    Nro = i + 1,
                    Fecha = p.FechaPago,
                    Cliente = string.IsNullOrWhiteSpace(p.Nombre) ? "—" : p.Nombre,
                    Monto = p.MontoPagado,
                    ReservaID = p.ReservaId,
                    ReservaCodigo = p.ReservaCodigo
                }).ToList(),
                //Pagos = pagos.Select((p, i) => new PagoLineaVM
                //{
                //    Nro = i + 1,
                //    Fecha = p.FechaPago,
                //    Cliente = string.IsNullOrWhiteSpace(p.Nombre) ? "—" : p.Nombre,
                //    Monto = p.MontoPagado
                //}).ToList(),
                Ajustes = ajustesDb.Select(a => new AjusteLineaVM
                {
                    CajaAjusteID = a.CajaAjusteID,
                    Fecha = a.Fecha,
                    TipoTxt = a.Tipo == 1 ? "Ingreso" : "Egreso",
                    Motivo = a.Motivo,
                    Monto = a.Monto
                }).ToList()
            };

            return PartialView("_DetalleCaja", vm);
        }

        [HttpGet("DetallePdf/{id:int}")]
        public async Task<IActionResult> DetallePdf(int id, bool descargar = false)
        {
            // Reutiliza el armado del VM de Detalle (mismo código que ya tienes arriba)
            var caja = await _db.cajas.AsNoTracking().FirstOrDefaultAsync(c => c.CajaID == id);
            if (caja == null) return NotFound();

            var pagoIds = await _db.cajaPagos
                .Where(p => p.CajaID == id)
                .OrderBy(p => p.CajaPagoID)
                .Select(p => p.PagoReservaID)
                .ToListAsync();

            var pagos = await _db.pagosReserva
                .Where(p => pagoIds.Contains(p.PagoReservaID))
                .Select(p => new {
                    p.PagoReservaID,
                    p.FechaPago,
                    p.MontoPagado,
                    p.TipoPagoID,
                    ReservaId = (int?)p.ReservaID,
                    ReservaCodigo = p.Reserva != null ? p.Reserva.Codigo : null,
                    Nombre = (p.Reserva != null
                        ? (p.Reserva.Cliente.Persona.PrimerNombre + " " + p.Reserva.Cliente.Persona.PrimerApellido).Trim()
                        : null)
                })
                .OrderBy(p => p.FechaPago)
                .ToListAsync();

            var resumen = new ResumenMedioVM();
            foreach (var p in pagos)
            {
                switch ((MedioPago)p.TipoPagoID)
                {
                    case MedioPago.Efectivo: resumen.Efectivo += p.MontoPagado; break;
                    case MedioPago.Transferencia: resumen.Transferencia += p.MontoPagado; break;
                    case MedioPago.Deposito: resumen.Deposito += p.MontoPagado; break;
                    case MedioPago.CompraClick: resumen.CompraClick += p.MontoPagado; break;
                    case MedioPago.PlataformaReservas: resumen.PlataformaReservas += p.MontoPagado; break;
                }
            }

            var ajustesDb = await _db.cajaAjustes
                .Where(a => a.CajaID == id)
                .OrderBy(a => a.Fecha)
                .ToListAsync();

            resumen.AjustesIngreso = ajustesDb.Where(a => a.Tipo == 1).Sum(a => a.Monto);
            resumen.AjustesEgreso = ajustesDb.Where(a => a.Tipo == 2).Sum(a => a.Monto);

            var vm = new DetalleCajaVM
            {
                CajaID = caja.CajaID,
                Codigo = caja.Codigo,
                FechaApertura = caja.FechaApertura,
                FechaCierre = caja.FechaCierre,
                UsuarioAperturaID = caja.UsuarioAperturaID,
                UsuarioCierreID = caja.UsuarioCierreID,
                MontoApertura = caja.MontoApertura,
                Resumen = resumen,
                Pagos = pagos.Select((p, i) => new PagoLineaVM
                {
                    Nro = i + 1,
                    Fecha = p.FechaPago,
                    Cliente = string.IsNullOrWhiteSpace(p.Nombre) ? "—" : p.Nombre,
                    Monto = p.MontoPagado,
                    ReservaID = p.ReservaId,
                    ReservaCodigo = p.ReservaCodigo
                }).ToList(),
                Ajustes = ajustesDb.Select(a => new AjusteLineaVM
                {
                    CajaAjusteID = a.CajaAjusteID,
                    Fecha = a.Fecha,
                    TipoTxt = a.Tipo == 1 ? "Ingreso" : "Egreso",
                    Motivo = a.Motivo,
                    Monto = a.Monto
                }).ToList()
            };

            var pdf = new ViewAsPdf("DetalleCajaPdf", vm)  // <--- crea esta vista
            {
                PageSize = Size.A4,
                PageOrientation = Orientation.Portrait,
                CustomSwitches = "--footer-center \"Página [page] de [toPage]\" --footer-font-size 8 --footer-spacing 5",
                ContentDisposition = descargar ? ContentDisposition.Attachment : ContentDisposition.Inline
            };
            if (descargar) pdf.FileName = $"Caja_{vm.Codigo}_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
            return pdf;
        }



        //[HttpGet("Detalle/{id:int}")]
        //public async Task<IActionResult> Detalle(int id)
        //{
        //    var caja = await _db.cajas.AsNoTracking()
        //                    .FirstOrDefaultAsync(c => c.CajaID == id);
        //    if (caja == null) return NotFound();

        //    // Ids de pagos asociados a esta caja
        //    var pagoIds = await _db.cajaPagos
        //        .Where(p => p.CajaID == id)
        //        .OrderBy(p => p.CajaPagoID)
        //        .Select(p => p.PagoReservaID)
        //        .ToListAsync();

        //    // Trae pagos con proyección del nombre del cliente
        //    // ADAPTA los nombres de navegación si en tu modelo son distintos
        //    var pagos = await _db.pagosReserva
        //        .Where(p => pagoIds.Contains(p.PagoReservaID))
        //        .Select(p => new
        //        {
        //            p.PagoReservaID,
        //            p.FechaPago,
        //            p.MontoPagado,
        //            p.TipoPagoID,

        //            // 1) si usas un nombre impreso en el comprobante
        //            Nombre = (p.Reserva != null
        //                         ? ((p.Reserva.Cliente.Persona.PrimerNombre.Trim() 
        //                         //+ " " + p.Reserva.Cliente.Persona.SegundoNombre).Trim()
        //                         + " " + p.Reserva.Cliente.Persona.PrimerApellido).Trim())
        //                         : null)
        //                     // 3) último recurso: algún campo de huésped/titular que tengas
        //                     //?? p.TitularNombre
        //        })
        //        .OrderBy(p => p.FechaPago)
        //        .ToListAsync();

        //    // Resumen por medio de pago
        //    var resumen = new ResumenMedioVM();
        //    foreach (var p in pagos)
        //    {
        //        switch ((MedioPago)p.TipoPagoID)
        //        {
        //            case MedioPago.Efectivo: resumen.Efectivo += p.MontoPagado; break;
        //            case MedioPago.Transferencia: resumen.Transferencia += p.MontoPagado; break;
        //            case MedioPago.Deposito: resumen.Deposito += p.MontoPagado; break;
        //            case MedioPago.CompraClick: resumen.CompraClick += p.MontoPagado; break;
        //            case MedioPago.PlataformaReservas: resumen.PlataformaReservas += p.MontoPagado; break;
        //        }
        //    }

        //    var vm = new DetalleCajaVM
        //    {
        //        CajaID = caja.CajaID,
        //        Codigo = caja.Codigo,
        //        FechaApertura = caja.FechaApertura,
        //        FechaCierre = caja.FechaCierre,
        //        UsuarioAperturaID = caja.UsuarioAperturaID,
        //        UsuarioCierreID = caja.UsuarioCierreID,
        //        MontoApertura = caja.MontoApertura,
        //        Resumen = resumen,
        //        Pagos = pagos.Select((p, i) => new PagoLineaVM
        //        {
        //            Nro = i + 1,
        //            Fecha = p.FechaPago,
        //            Cliente = string.IsNullOrWhiteSpace(p.Nombre) ? "—" : p.Nombre,
        //            Monto = p.MontoPagado
        //        }).ToList()
        //    };

        //    return PartialView("_DetalleCaja", vm);
        //}

        //[HttpGet("Detalle/{id:int}")]
        //public async Task<IActionResult> Detalle(int id)
        //{
        //    var caja = await _db.cajas.AsNoTracking().FirstOrDefaultAsync(c => c.CajaID == id);
        //    if (caja == null) return NotFound();

        //    // pagos vinculados a esta caja
        //    var pagoIds = await _db.cajaPagos
        //        .Where(p => p.CajaID == id)
        //        .OrderBy(p => p.CajaPagoID)
        //        .Select(p => p.PagoReservaID)
        //        .ToListAsync();

        //    // Traer pagos reales (AJUSTA los selects a tu entidad)
        //    var pagos = await _db.pagosReserva
        //        .Where(p => pagoIds.Contains(p.PagoReservaID))
        //        .OrderBy(p => p.FechaPago)
        //        .ToListAsync();

        //    var resumen = new ResumenMedioVM();
        //    foreach (var p in pagos)
        //    {
        //        switch (p.TipoPagoID)
        //        {
        //            case (short)MedioPago.Efectivo: resumen.Efectivo += p.MontoPagado; break;
        //            case (short)MedioPago.Transferencia: resumen.Transferencia += p.MontoPagado; break;
        //            case (short)MedioPago.Deposito: resumen.Deposito += p.MontoPagado; break;
        //            case (short)MedioPago.CompraClick: resumen.CompraClick += p.MontoPagado; break;
        //            case (short)MedioPago.PlataformaReservas: resumen.PlataformaReservas += p.MontoPagado; break;
        //        }
        //    }

        //    var vm = new DetalleCajaVM
        //    {
        //        CajaID = caja.CajaID,
        //        Codigo = caja.Codigo,
        //        FechaApertura = caja.FechaApertura,
        //        FechaCierre = caja.FechaCierre,
        //        UsuarioAperturaID = caja.UsuarioAperturaID,
        //        UsuarioCierreID = caja.UsuarioCierreID,
        //        MontoApertura = caja.MontoApertura,
        //        Resumen = resumen,
        //        Pagos = pagos.Select((p, i) => new PagoLineaVM
        //        {
        //            Nro = i + 1,
        //            Fecha = p.FechaPago,
        //            Cliente = p.ComprobanteNombre, // ajusta a tu modelo
        //            Monto = p.MontoPagado
        //        }).ToList()
        //    };

        //    return PartialView("_DetalleCaja", vm);
        //}

        // CERRAR
        [HttpPost("Cerrar/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cerrar(int id)
        {
            var caja = await _db.cajas.FirstOrDefaultAsync(c => c.CajaID == id);
            if (caja == null) return NotFound();
            if (caja.EstadoCajaID == 2) return BadRequest("La caja ya está cerrada.");

            caja.EstadoCajaID = 2;
            caja.FechaCierre = DateTime.Now;
            caja.UsuarioCierreID = GetUserId();

            await _db.SaveChangesAsync();
            return Json(new { ok = true, redirectUrl = Url.Action("Index", "Caja") });
        }

        //[HttpPost("Cerrar/{id:int}")]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Cerrar(int id)
        //{
        //    var caja = await _db.cajas.FirstOrDefaultAsync(c => c.CajaID == id);
        //    if (caja == null) return NotFound();
        //    if (caja.EstadoCajaID == 2) return BadRequest("La caja ya está cerrada.");

        //    caja.EstadoCajaID = 2; // Cerrada
        //    caja.FechaCierre = DateTime.Now;
        //    caja.UsuarioCierreID = GetUserId();                  // <-- ID
        //    //caja.UsuarioCierreID = User?.Identity?.Name ?? "usuario";

        //    await _db.SaveChangesAsync();
        //    return Json(new { ok = true });
        //}

        // Vincular un pago de reserva a la caja abierta (llámalo cuando registras un pago)
        [HttpPost("AttachPago")]
        public async Task<IActionResult> AttachPago(int pagoReservaId)
        {
            var caja = await _db.cajas.OrderByDescending(c => c.CajaID).FirstOrDefaultAsync(c => c.EstadoCajaID == 1);
            if (caja == null) return BadRequest("No hay caja abierta.");

            // verifica que el pago exista (opcional)
            var existe = await _db.pagosReserva.AnyAsync(p => p.PagoReservaID == pagoReservaId);
            if (!existe) return NotFound("Pago no encontrado.");

            _db.cajaPagos.Add(new CajaPago
            {
                CajaID = caja.CajaID,
                PagoReservaID = pagoReservaId,
                FechaRegistro = DateTime.Now
            });
            await _db.SaveChangesAsync();
            return Ok();
        }

        private string GetUserId()
        {
            // Busca el claim estándar de ID; si no existe, intenta con "sub" o "userid"
            return User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue("sub")
                ?? User.FindFirstValue("userid")
                ?? User.Identity?.Name
                ?? "usuario";
        }

        [HttpGet("ExportPdf")]
        public async Task<IActionResult> ExportPdf(string? Codigo, string? Usuario, short? EstadoCajaID,
    DateTime? FechaDesde, DateTime? FechaHasta, bool descargar = false)
        {
            var q = _db.cajas.Include(c => c.EstadoCaja).AsNoTracking().AsQueryable();

            //forzar solo cajas del usuario logueado
            var userId = GetUserId();
            q = q.Where(c => c.UsuarioAperturaID == userId);

            if (!string.IsNullOrWhiteSpace(Codigo)) q = q.Where(c => c.Codigo.Contains(Codigo));
            if (EstadoCajaID.HasValue && EstadoCajaID.Value > 0) q = q.Where(c => c.EstadoCajaID == EstadoCajaID.Value);
            if (FechaDesde.HasValue) q = q.Where(c => c.FechaApertura >= FechaDesde.Value.Date);
            if (FechaHasta.HasValue) q = q.Where(c => c.FechaApertura <= FechaHasta.Value.Date.AddDays(1).AddTicks(-1));

            var model = await q.OrderByDescending(c => c.CajaID).ToListAsync();

            ViewBag.Codigo = Codigo;
            ViewBag.Usuario = userId;              // muestra quién está filtrando
            ViewBag.EstadoCajaID = EstadoCajaID;
            ViewBag.FechaDesde = FechaDesde;
            ViewBag.FechaHasta = FechaHasta;

            var pdf = new ViewAsPdf("ReportePdf", model)
            {
                PageSize = Size.A4,
                PageOrientation = Orientation.Portrait,
                CustomSwitches = "--footer-center \"Página [page] de [toPage]\" --footer-font-size 8 --footer-spacing 5",
                ContentDisposition = descargar ? ContentDisposition.Attachment : ContentDisposition.Inline
            };
            if (descargar) pdf.FileName = $"Cajas_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
            return pdf;
        }



        //[HttpGet("ExportPdf")]
        //public async Task<IActionResult> ExportPdf(string? Codigo, string? Usuario, short? EstadoCajaID,
        //                                   DateTime? FechaDesde, DateTime? FechaHasta,
        //                                   bool descargar = false)
        //{
        //    var q = _db.cajas.Include(c => c.EstadoCaja).AsNoTracking().AsQueryable();
        //    if (!string.IsNullOrWhiteSpace(Codigo)) q = q.Where(c => c.Codigo.Contains(Codigo));
        //    if (!string.IsNullOrWhiteSpace(Usuario)) q = q.Where(c => c.UsuarioAperturaID.Contains(Usuario));
        //    if (EstadoCajaID.HasValue && EstadoCajaID.Value > 0) q = q.Where(c => c.EstadoCajaID == EstadoCajaID.Value);
        //    if (FechaDesde.HasValue) q = q.Where(c => c.FechaApertura >= FechaDesde.Value.Date);
        //    if (FechaHasta.HasValue) q = q.Where(c => c.FechaApertura <= FechaHasta.Value.Date.AddDays(1).AddTicks(-1));

        //    var model = await q.OrderByDescending(c => c.CajaID).ToListAsync();

        //    // TODO: crea una vista "ReportePdf.cshtml" para Caja y pásale 'model'
        //    var pdf = new ViewAsPdf("ReportePdf", model)
        //    {
        //        PageSize = Size.A4,
        //        PageOrientation = Orientation.Portrait,
        //        CustomSwitches = "--footer-center \"Página [page] de [toPage]\" --footer-font-size 8 --footer-spacing 5",
        //        ContentDisposition = descargar ? ContentDisposition.Attachment : ContentDisposition.Inline
        //    };
        //    if (descargar) pdf.FileName = $"Caja_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
        //    return pdf;
        //}

        //MINI ENDPOINT PARA EL MENÚ (BLOQUEAR RESERVAS SI NO EXISTE CAJA ABIERTA)
        [HttpGet("IsOpen")]
        public async Task<IActionResult> IsOpen()
        {
            var abierta = await _db.cajas.AnyAsync(c => c.EstadoCajaID == 1 && c.UsuarioAperturaID.Contains(GetUserId()));
            return Json(new { abierta });
        }

        // Agrega esta clase (por ejemplo, encima del controller o en la misma región de POSTs)
        public class CajaAjustePost
        {
            public int CajaID { get; set; }
            public short Tipo { get; set; }        // 1 = Ingreso, 2 = Egreso
            public decimal Monto { get; set; }
            public string Motivo { get; set; } = "";
        }

        // En CajaController.cs
        [HttpPost("AgregarAjuste")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AgregarAjuste(CajaAjustePost m)
        {
            // Validaciones básicas
            if (m.CajaID <= 0) return BadRequest("Caja inválida.");
            if (m.Tipo != 1 && m.Tipo != 2) return BadRequest("Tipo inválido.");
            if (m.Monto <= 0) return BadRequest("El monto debe ser > 0.");
            if (string.IsNullOrWhiteSpace(m.Motivo)) return BadRequest("Motivo requerido.");

            // Caja existente y abierta
            var caja = await _db.cajas.FirstOrDefaultAsync(c => c.CajaID == m.CajaID);
            if (caja == null) return NotFound("Caja no encontrada.");
            if (caja.EstadoCajaID == 2) return BadRequest("La caja está cerrada.");

            // Guardar ajuste
            _db.cajaAjustes.Add(new CajaAjuste
            {
                CajaID = m.CajaID,
                Tipo = m.Tipo,              // 1=Ingreso (+), 2=Egreso (−)
                Monto = m.Monto,
                Motivo = m.Motivo.Trim(),
                Fecha = DateTime.Now,
                UsuarioID = GetUserId()
            });
            await _db.SaveChangesAsync();

            // Sin RenderViewAsync: el cliente volverá a pedir /Caja/Detalle/{id}
            return Json(new { ok = true, cajaId = m.CajaID });
        }


    }
}