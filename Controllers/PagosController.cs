using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PIOGHOASIS.Infraestructure.Data;
using PIOGHOASIS.Models;
using System.Security.Claims;

namespace PIOGHOASIS.Controllers
{
    [Route("Pagos")]
    public class PagosController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        public PagosController(AppDbContext db, IWebHostEnvironment env)
        { _db = db; _env = env; }

        [HttpGet("Nueva")]
        public async Task<IActionResult> Nueva(int reservaId)
        {
            var r = await _db.reservas
                .Include(x => x.Cliente).ThenInclude(c => c.Persona)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ReservaID == reservaId);
            if (r == null) return NotFound();

            ViewBag.TiposPago = await _db.tiposPago.Where(x => x.Estado).OrderBy(x => x.Nombre).ToListAsync();
            ViewBag.FormasPago = await _db.formasPago.Where(x => x.Estado).OrderBy(x => x.Nombre).ToListAsync();
            ViewBag.Plataformas = await _db.plataformasReserva.Where(x => x.Estado).OrderBy(x => x.Nombre).ToListAsync();

            // cálculo de pagado/pendiente
            var pagado = await _db.pagosReserva.Where(p => p.ReservaID == reservaId).SumAsync(p => (decimal?)p.MontoPagado) ?? 0m;
            ViewBag.Pagado = pagado;
            ViewBag.Pendiente = Math.Max(0m, r.Total - pagado);

            return View(r); // tu vista elegante de “registro de pago”
        }

        public class PagoReservaPost
        {
            public int ReservaID { get; set; }
            public short FormaPagoID { get; set; }       // 1=Completo, 2=Anticipo
            public short TipoPagoID { get; set; }        // Efectivo/Transferencia/Depósito/Plataforma...
            public short? PlataformaID { get; set; }     // requerido si TipoPago = Plataforma / Booking / etc.
            public DateTime FechaPago { get; set; }
            public string? NumeroReferencia { get; set; } // requerido salvo Efectivo
            public decimal MontoPagado { get; set; }
            public string? Observaciones { get; set; }

            public IFormFile? Comprobante { get; set; }  // PDF/imagen opcional u obligatorio según tipo
        }

        [HttpPost("Crear")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(PagoReservaPost m)
        {
            // --- Validaciones de negocio ---
            var reserva = await _db.reservas.AsNoTracking().FirstOrDefaultAsync(x => x.ReservaID == m.ReservaID);
            if (reserva == null) return NotFound("Reserva no encontrada.");

            var totalPagado = await _db.pagosReserva.Where(p => p.ReservaID == m.ReservaID)
                .SumAsync(p => (decimal?)p.MontoPagado) ?? 0m;
            var pendiente = Math.Max(0m, reserva.Total - totalPagado);

            if (m.FormaPagoID == 1) // Completo
            {
                if (m.MontoPagado != pendiente)
                    ModelState.AddModelError(nameof(m.MontoPagado), $"El monto debe ser exactamente el pendiente ({pendiente:N2}).");
            }
            else // Anticipo
            {
                if (m.MontoPagado <= 0 || m.MontoPagado > pendiente)
                    ModelState.AddModelError(nameof(m.MontoPagado), $"El monto debe ser > 0 y <= pendiente ({pendiente:N2}).");
            }

            // Reglas por tipo de pago
            var tipo = await _db.tiposPago.AsNoTracking().FirstOrDefaultAsync(x => x.TipoPagoID == m.TipoPagoID);
            if (tipo == null) ModelState.AddModelError(nameof(m.TipoPagoID), "Tipo de pago inválido.");

            bool requiereRef = tipo != null && !string.Equals(tipo.Nombre, "Efectivo", StringComparison.OrdinalIgnoreCase);
            bool requiereArchivo = tipo != null && (
                   tipo.Nombre.Contains("Transferencia", StringComparison.OrdinalIgnoreCase)
                || tipo.Nombre.Contains("Deposito", StringComparison.OrdinalIgnoreCase)
                || tipo.Nombre.Contains("Plataforma", StringComparison.OrdinalIgnoreCase)
            );

            if (requiereRef && string.IsNullOrWhiteSpace(m.NumeroReferencia))
                ModelState.AddModelError(nameof(m.NumeroReferencia), "Número de referencia es requerido.");

            if (requiereArchivo && m.Comprobante == null)
                ModelState.AddModelError(nameof(m.Comprobante), "Debe adjuntar el comprobante.");

            if (!ModelState.IsValid)
            {
                // Vuelve a cargar combos
                ViewBag.TiposPago = await _db.tiposPago.Where(x => x.Estado).OrderBy(x => x.Nombre).ToListAsync();
                ViewBag.FormasPago = await _db.formasPago.Where(x => x.Estado).OrderBy(x => x.Nombre).ToListAsync();
                ViewBag.Plataformas = await _db.plataformasReserva.Where(x => x.Estado).OrderBy(x => x.Nombre).ToListAsync();

                // Y devolver la misma vista con errores
                var rvm = await _db.reservas
                    .Include(x => x.Cliente).ThenInclude(c => c.Persona)
                    .FirstAsync(x => x.ReservaID == m.ReservaID);
                var pagado = await _db.pagosReserva.Where(p => p.ReservaID == m.ReservaID).SumAsync(p => (decimal?)p.MontoPagado) ?? 0m;
                ViewBag.Pagado = pagado;
                ViewBag.Pendiente = Math.Max(0m, rvm.Total - pagado);
                return View("Nueva", rvm);
            }

            // --- Guardar archivo (si viene) ---
            string? relPath = null, fileName = null, mime = null;
            if (m.Comprobante != null && m.Comprobante.Length > 0)
            {
                var folder = Path.Combine(_env.WebRootPath, "uploads", "pagos", reserva.Codigo);
                Directory.CreateDirectory(folder);
                var safeName = Path.GetFileNameWithoutExtension(m.Comprobante.FileName);
                var ext = Path.GetExtension(m.Comprobante.FileName);
                fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}{ext}";
                var full = Path.Combine(folder, fileName);
                using (var fs = System.IO.File.Create(full))
                    await m.Comprobante.CopyToAsync(fs);

                relPath = $"/uploads/pagos/{reserva.Codigo}/{fileName}";
                mime = m.Comprobante.ContentType;
            }

            // --- Insert ---
            var pago = new PagoReserva
            {
                ReservaID = m.ReservaID,
                FormaPagoID = m.FormaPagoID,
                TipoPagoID = m.TipoPagoID,
                PlataformaID = m.PlataformaID,
                FechaPago = m.FechaPago.Date.Add(DateTime.Now.TimeOfDay),
                NumeroReferencia = m.NumeroReferencia,
                MontoPagado = m.MontoPagado,
                Observaciones = m.Observaciones,
                ComprobantePath = relPath,
                ComprobanteNombre = fileName,
                ComprobanteMime = mime
            };
            _db.pagosReserva.Add(pago);
            await _db.SaveChangesAsync();

            //=================== Vincular a caja abierta
            //await new CajaController(_db).AttachPago(pago.PagoReservaID);
            // o directamente:
            var cajaAbierta = await _db.cajas
            .Where(c => c.EstadoCajaID == 1 && c.UsuarioAperturaID == GetUserId()) // <-- del mismo usuario
            .Select(c => c.CajaID)
            .FirstOrDefaultAsync();

            if (cajaAbierta == 0)
            {
                // Opcional: decide si debes fallar o solo continuar sin vincular
                // ModelState.AddModelError("", "No tienes una caja abierta.");
                // return ...;
            }
            else
            {
                _db.cajaPagos.Add(new CajaPago
                {
                    CajaID = cajaAbierta,
                    PagoReservaID = pago.PagoReservaID,
                    FechaRegistro = DateTime.Now
                });
                await _db.SaveChangesAsync();
            }

            // --- Recalcular estado ---
            await RecalcularEstadoReservaAsync(m.ReservaID);

            // Redirige a Detalles de la reserva
            //return RedirectToAction("Index", "Reservas", new { id = m.ReservaID });

            TempData["FlashModal"] = "Pago registrado correctamente.";
            TempData["FlashType"] = "success"; // opcional: success | warning | info | danger
            return RedirectToAction("DetalleReserva", "Pagos", new { reservaId = m.ReservaID });
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

        [HttpGet("DescargarComprobante/{pagoId:int}")]
        public async Task<IActionResult> DescargarComprobante(int pagoId)
        {
            var p = await _db.pagosReserva.AsNoTracking().FirstOrDefaultAsync(x => x.PagoReservaID == pagoId);
            if (p == null || string.IsNullOrWhiteSpace(p.ComprobantePath))
                return NotFound();

            var full = Path.Combine(_env.WebRootPath, p.ComprobantePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (!System.IO.File.Exists(full)) return NotFound();

            var ct = string.IsNullOrWhiteSpace(p.ComprobanteMime) ? "application/octet-stream" : p.ComprobanteMime;
            var fn = string.IsNullOrWhiteSpace(p.ComprobanteNombre) ? Path.GetFileName(full) : p.ComprobanteNombre;
            var bytes = await System.IO.File.ReadAllBytesAsync(full);
            return File(bytes, ct, fn);
        }

        //pagos
        private async Task RecalcularEstadoReservaAsync(int reservaId)
        {
            var r = await _db.reservas
                .AsNoTracking()
                .FirstAsync(x => x.ReservaID == reservaId);

            var pagado = await _db.pagosReserva
                .Where(p => p.ReservaID == reservaId)
                .SumAsync(p => (decimal?)p.MontoPagado) ?? 0m;

            if (pagado >= r.Total)
            {
                // Confirmada
                var idConfirmada = await _db.estadosReserva
                    .Where(e => e.Codigo == "ESTHAB0002")
                    .Select(e => e.EstadoReservaID)
                    .FirstAsync();

                var toUpdate = new Reserva { ReservaID = reservaId, EstadoReservaID = idConfirmada, UsuarioFinaliza = GetUserId() };
                _db.Attach(toUpdate);
                _db.Entry(toUpdate).Property(x => x.EstadoReservaID).IsModified = true;
                _db.Entry(toUpdate).Property(x => x.UsuarioFinaliza).IsModified = true;
                await _db.SaveChangesAsync();
            }
            else
            {
                // Sigue en Reservada (no tocamos si ya está “Confirmada” o “Cancelada”)
                // Si deseas “forzar” reservada cuando pagado>0 && <total, descomenta:

                // var idReservada = await _db.estadosReserva
                //     .Where(e => e.Codigo == "ESTHAB0001")
                //     .Select(e => e.EstadoReservaID)
                //     .FirstAsync();
                // var toUpdate = new Reserva { ReservaID = reservaId, EstadoReservaID = idReservada };
                // _db.Attach(toUpdate);
                // _db.Entry(toUpdate).Property(x => x.EstadoReservaID).IsModified = true;
                // await _db.SaveChangesAsync();
            }
        }

        [HttpGet("DetalleReserva/{reservaId:int}")]
        public async Task<IActionResult> DetalleReserva(int reservaId, int? pagoId = null)
        {
            var reserva = await _db.reservas
                .Include(r => r.Cliente).ThenInclude(c => c.Persona)
                .Include(r => r.Estado)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.ReservaID == reservaId);

            if (reserva == null) return NotFound();

            var pagos = await _db.pagosReserva
                .Include(x => x.FormaPago)
                .Include(x => x.TipoPago)
                .Include(x => x.Plataforma)
                .Where(x => x.ReservaID == reservaId)
                .OrderByDescending(x => x.PagoReservaID)
                .AsNoTracking()
                .ToListAsync();

            var pagado = pagos.Sum(x => x.MontoPagado);
            var pendiente = Math.Max(0m, reserva.Total - pagado);

            var seleccionado = pagoId.HasValue
                ? pagos.FirstOrDefault(x => x.PagoReservaID == pagoId.Value)
                : pagos.FirstOrDefault(); // último por defecto

            var vm = new PIOGHOASIS.Models.ViewModels.PagoDetalleReservaVM
            {
                Reserva = reserva,
                Pagos = pagos,
                Seleccionado = seleccionado,
                Pagado = pagado,
                Pendiente = pendiente
            };

            return View("DetalleReserva", vm);
        }

        [HttpGet("ComprobanteParcial/{pagoId:int}")]
        public async Task<IActionResult> ComprobanteParcial(int pagoId)
        {
            var sel = await _db.pagosReserva
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.PagoReservaID == pagoId);

            // Puedes devolver un Model == null y que el partial muestre el mensaje por defecto
            if (sel == null)
                return PartialView("_ComprobantePago", null);

            return PartialView("_ComprobantePago", sel);
        }
    }
}
