using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PIOGHOASIS.Infraestructure.Data;
using PIOGHOASIS.Models.ViewModels;
using Rotativa.AspNetCore;
using Rotativa.AspNetCore.Options;
using System.Text;

namespace PIOGHOASIS.Controllers
{
    [Route("Reportes")]
    public class ReportesController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        //public ReportesController(AppDbContext db) => _db = db;
        public ReportesController(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        // helper para convertir a file:///
        private string FileUrl(params string[] parts)
        {
            var phys = Path.Combine(parts);
            var norm = phys.Replace("\\", "/");
            return "file:///" + norm;
        }


        // ====== Construcción del VM Reporte Clientes ======
        private async Task<ReporteClientesVM> BuildReporteClientes(DateTime? desde, DateTime? hasta, bool soloActivos)
        {
            var vm = new ReporteClientesVM
            {
                Desde = (desde ?? DateTime.Today.AddMonths(-1)).Date,
                Hasta = (hasta ?? DateTime.Today).Date,
                SoloActivos = soloActivos
            };

            var d = vm.Desde!.Value.Date;
            var h = vm.Hasta!.Value.Date.AddDays(1); // exclusivo

            // Base de clientes (con Persona)
            var qCli = _db.clientes
                .Include(c => c.Persona)
                .AsNoTracking();

            if (soloActivos)
                qCli = qCli.Where(c => c.Estado);

            var clientes = await qCli.ToListAsync();

            vm.TotalClientes = clientes.Count;
            vm.NuevosEnRango = clientes.Count(c => c.Persona.FechaRegistro >= d && c.Persona.FechaRegistro < h);
            vm.PaisTopCount = clientes.Select(c => c.Persona.PaisID).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Count();

            // Reservas y pagos en el rango
            var reservasRango = await _db.reservas
                .Include(r => r.Cliente).ThenInclude(c => c.Persona)
                .AsNoTracking()
                .Where(r => r.FechaCheckIn < h && r.FechaCheckOut >= d) // solape con rango
                .ToListAsync();

            var pagosRango = await _db.pagosReserva
                .Include(p => p.Reserva).ThenInclude(r => r.Cliente).ThenInclude(c => c.Persona)
                .AsNoTracking()
                .Where(p => p.FechaPago >= d && p.FechaPago < h)
                .ToListAsync();

            // Clientes con al menos una reserva en el rango
            var clientesConReserva = reservasRango
                .Select(r => r.Cliente.ClienteID)
                .Distinct()
                .ToHashSet();
            vm.ClientesConReserva = clientesConReserva.Count;

            // Repetidores en el rango: 2+ reservas
            vm.Repetidores = reservasRango
                .GroupBy(r => r.Cliente.ClienteID)
                .Count(g => g.Count() >= 2);

            // Ticket promedio por cliente (usando pagos del rango)
            var gastadoPorCliente = pagosRango
                .GroupBy(p => p.Reserva.Cliente.ClienteID)
                .Select(g => new { ClienteID = g.Key, Total = g.Sum(x => x.MontoPagado) })
                .ToList();

            var mapDeptos = await _db.departamentos
            .AsNoTracking()
            .ToDictionaryAsync(x => x.DepartamentoID, x => x.Nombre);

            vm.TicketPromedioCliente = gastadoPorCliente.Count == 0
                ? 0
                : gastadoPorCliente.Sum(x => x.Total) / gastadoPorCliente.Count;

            // ===== Por País =====
            vm.PorPais = clientes
                .GroupBy(c => string.IsNullOrWhiteSpace(c.Persona.PaisID) ? "—" : c.Persona.PaisID!.ToUpper())
                .Select(g => new ItemClaveCount(g.Key, g.Count()))
                .OrderByDescending(x => x.Conteo)
                .ToList();

            // ===== Por Departamento (solo GTM) =====
            vm.PorDepartamento = clientes
            .Where(c => (c.Persona.PaisID ?? "").ToUpper() == "GTM" && c.Persona.DepartamentoID.HasValue)
            .GroupBy(c => c.Persona.DepartamentoID!.Value)
            .Select(g => new ItemClaveCount(
                mapDeptos.ContainsKey(g.Key) ? mapDeptos[g.Key] : $"Depto {g.Key}",
                g.Count()
            ))
            .OrderByDescending(x => x.Conteo)
            .Take(10)
            .ToList();

            //vm.PorDepartamento = clientes
            //    .Where(c => (c.Persona.PaisID ?? "").ToUpper() == "GTM" && c.Persona.DepartamentoID.HasValue)
            //    .GroupBy(c => c.Persona.DepartamentoID!.Value)
            //    .Select(g => new ItemClaveCount("Depto " + g.Key, g.Count()))
            //    .OrderByDescending(x => x.Conteo)
            //    .Take(10) // top 10
            //    .ToList();

            // ===== Edades (buckets) =====
            int Edad(DateTime? fn)
            {
                if (fn == null) return -1;
                var today = DateTime.Today;
                int age = today.Year - fn.Value.Year;
                if (fn.Value.Date > today.AddYears(-age)) age--;
                return age;
            }
            string BucketEdad(int e) => e < 0 ? "N/D"
                : e < 18 ? "<18"
                : e <= 24 ? "18–24"
                : e <= 34 ? "25–34"
                : e <= 44 ? "35–44"
                : e <= 54 ? "45–54"
                : e <= 64 ? "55–64"
                : "65+";

            vm.Edades = clientes
                .Select(c => BucketEdad(Edad(c.Persona.FechaNacimiento)))
                .GroupBy(s => s)
                .Select(g => new ItemClaveCount(g.Key, g.Count()))
                .OrderBy(s => s.Clave) // orden aproximado
                .ToList();

            // ===== Altas por mes (en el rango) =====
            vm.NuevosPorMes = clientes
                .Where(c => c.Persona.FechaRegistro >= d && c.Persona.FechaRegistro < h)
                .GroupBy(c => new DateTime(c.Persona.FechaRegistro!.Value.Year, c.Persona.FechaRegistro!.Value.Month, 1))
                .Select(g => new ItemFechaCount(g.Key, g.Count()))
                .OrderBy(x => x.Fecha)
                .ToList();

            // ===== Top clientes por gasto (en el rango) =====
            var top = pagosRango
                .GroupBy(p => new {
                    p.Reserva.Cliente.ClienteID,
                    p.Reserva.Cliente.Persona.PrimerNombre,
                    p.Reserva.Cliente.Persona.SegundoNombre,
                    p.Reserva.Cliente.Persona.PrimerApellido,
                    p.Reserva.Cliente.Persona.SegundoApellido,
                    DPI = p.Reserva.Cliente.Persona.NumeroDocumento
                })
                .Select(g => new TopClienteRow(
                    g.Key.ClienteID,
                    ((g.Key.PrimerNombre ?? "") + " " + (g.Key.SegundoNombre ?? "") + " " + (g.Key.PrimerApellido ?? "") + " " + (g.Key.SegundoApellido ?? "")).Replace("  ", " ").Trim(),
                    g.Key.DPI,
                    g.Sum(x => x.MontoPagado),
                    g.Select(x => x.ReservaID).Distinct().Count(),   // si tu entidad de pago tiene ReservaID
                    g.Max(x => (DateTime?)x.FechaPago)
                ))
                .OrderByDescending(x => x.TotalPagado)
                .Take(10)
                .ToList();
            vm.TopClientes = top;

            // ===== Detalle =====
            var pagosPorClienteTodo = pagosRango
                .GroupBy(p => p.Reserva.Cliente.ClienteID)
                .ToDictionary(g => g.Key, g => new {
                    Total = g.Sum(x => x.MontoPagado),
                    Ult = g.Max(x => (DateTime?)x.FechaPago)
                });

            var reservasPorCliente = reservasRango
                .GroupBy(r => r.Cliente.ClienteID)
                .ToDictionary(g => g.Key, g => g.Count());

            vm.Detalle = clientes.Select(c => {
                var nombre = ((c.Persona.PrimerNombre ?? "") + " " + (c.Persona.SegundoNombre ?? "") + " " +
                              (c.Persona.PrimerApellido ?? "") + " " + (c.Persona.SegundoApellido ?? "")).Replace("  ", " ").Trim();
                pagosPorClienteTodo.TryGetValue(c.ClienteID, out var pg);
                reservasPorCliente.TryGetValue(c.ClienteID, out var rc);
                return new ClienteDetalleRow(
                    c.ClienteID, nombre, c.Persona.NumeroDocumento, c.Persona.PaisID,
                    c.Persona.FechaRegistro, rc, pg?.Total ?? 0, pg?.Ult
                );
            })
            .OrderByDescending(x => x.TotalPagado)
            .ToList();

            return vm;
        }

        // ====== HTML ======
        [HttpGet("ClientesReporte")]
        public async Task<IActionResult> ClientesReporte(DateTime? desde, DateTime? hasta, bool soloActivos = true)
        {
            var vm = await BuildReporteClientes(desde, hasta, soloActivos);
            return View(vm); // Vista: Views/Reportes/ClientesReporte.cshtml (opcional si la quieres)
        }

        // ====== PDF ======
        [HttpGet("ClientesReportePdf")]
        public async Task<IActionResult> ClientesReportePdf(DateTime? desde, DateTime? hasta, bool soloActivos = true)
        {
            var vm = await BuildReporteClientes(desde, hasta, soloActivos);

            var switches = string.Join(" ",
                "--enable-javascript",
                "--no-stop-slow-scripts",
                "--javascript-delay 300",
                "--viewport-size 1280x1024",
                "--dpi 180",
                "--footer-center \"Página [page] de [toPage]\"",
                "--footer-font-size 9",
                "--footer-spacing 4"
            );

            return new Rotativa.AspNetCore.ViewAsPdf("ClientesReportePdf", vm)
            {
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,
                CustomSwitches = switches
            };
        }



        // ====== Construcción del VM (compartido HTML + PDF) ======
        private async Task<ReporteIngresosVM> BuildReporteIngresos(
    DateTime? desde, DateTime? hasta, short? tipoPagoId, short? formaPagoId, short? plataformaId)
        {
            var vm = new ReporteIngresosVM
            {
                Desde = (desde ?? DateTime.Today).Date,
                Hasta = (hasta ?? DateTime.Today).Date,
                TipoPagoId = tipoPagoId,
                FormaPagoId = formaPagoId,
                PlataformaId = plataformaId
            };

            var d = vm.Desde!.Value.Date;
            var h = vm.Hasta!.Value.Date.AddDays(1); // exclusivo

            // 1) Query base
            var q = _db.pagosReserva
                .Include(p => p.TipoPago)
                .Include(p => p.FormaPago)
                .Include(p => p.Plataforma)
                .Include(p => p.Reserva)
                    .ThenInclude(r => r.Cliente)
                        .ThenInclude(c => c.Persona)
                .AsNoTracking()
                .Where(p => p.FechaPago >= d && p.FechaPago < h);

            // 2) Filtros opcionales
            if (tipoPagoId.HasValue) q = q.Where(p => p.TipoPagoID == tipoPagoId.Value);
            if (formaPagoId.HasValue) q = q.Where(p => p.FormaPagoID == formaPagoId.Value);
            if (plataformaId.HasValue) q = q.Where(p => p.PlataformaID == plataformaId.Value);

            // 3) Ejecutar la consulta
            var pagos = await q.OrderByDescending(x => x.FechaPago).ToListAsync();

            // 4) KPIs
            vm.TotalCobrado = pagos.Sum(x => x.MontoPagado);
            vm.CantPagos = pagos.Count;
            vm.TicketPromedio = vm.CantPagos == 0 ? 0 : vm.TotalCobrado / vm.CantPagos;

            // 5) Tabla
            vm.Pagos = pagos.Select(p => new PagoRowIng(
                p.FechaPago,
                p.TipoPago?.Nombre ?? "—",
                p.FormaPago?.Nombre ?? "—",
                p.Plataforma?.Nombre,
                p.Reserva.Codigo,
                $"{p.Reserva.Cliente.Persona.PrimerNombre} {p.Reserva.Cliente.Persona.PrimerApellido}".Trim(),
                p.MontoPagado,
                p.NumeroReferencia
            )).ToList();

            // 6) Dashboards
            vm.PorTipoPago = pagos
                .GroupBy(p => p.TipoPago?.Nombre ?? "—")
                .Select(g => new ItemMonto(g.Key, g.Sum(x => x.MontoPagado)))
                .OrderByDescending(x => x.Monto)
                .ToList();

            vm.PorFormaPago = pagos
                .GroupBy(p => p.FormaPago?.Nombre ?? "—")
                .Select(g => new ItemMonto(g.Key, g.Sum(x => x.MontoPagado)))
                .OrderByDescending(x => x.Monto)
                .ToList();

            vm.PorPlataforma = pagos
                .GroupBy(p => p.Plataforma?.Nombre ?? "—")
                .Select(g => new ItemMonto(g.Key, g.Sum(x => x.MontoPagado)))
                .OrderByDescending(x => x.Monto)
                .ToList();

            vm.PorDia = pagos
                .GroupBy(p => p.FechaPago.Date)
                .Select(g => new ItemMontoDate(g.Key, g.Sum(x => x.MontoPagado)))
                .OrderBy(x => x.Dia)
                .ToList();

            return vm;
        }


        // ====== HTML ======
        [HttpGet("PagosIngresos")]
        public async Task<IActionResult> PagosIngresos(DateTime? desde, DateTime? hasta, short? tipoPagoId, short? formaPagoId, short? plataformaId)
        {
            // Combos
            ViewBag.TiposPago = await _db.tiposPago.Where(x => x.Estado).OrderBy(x => x.Nombre).ToListAsync();
            ViewBag.FormasPago = await _db.formasPago.Where(x => x.Estado).OrderBy(x => x.Nombre).ToListAsync();
            ViewBag.Plataformas = await _db.plataformasReserva.Where(x => x.Estado).OrderBy(x => x.Nombre).ToListAsync();

            var vm = await BuildReporteIngresos(desde, hasta, tipoPagoId, formaPagoId, plataformaId);
            return View(vm);
        }

        // ====== PDF ======
        [HttpGet("PagosIngresosPdf")]
        public async Task<IActionResult> PagosIngresosPdf(
    DateTime? desde, DateTime? hasta, short? tipoPagoId, short? formaPagoId, short? plataformaId)
        {
            var vm = await BuildReporteIngresos(desde, hasta, tipoPagoId, formaPagoId, plataformaId);

            var webRoot = _env.WebRootPath;
            var logoPath = Path.Combine(webRoot, "img", "login", "logo-oasis.png");

            // 👇 Intenta con y sin "dist"
            var chartCandidates = new[]
            {
        Path.Combine(webRoot, "lib", "chartjs-2", "Chart.min.js"),           // <- como lo tienes ahora
        Path.Combine(webRoot, "lib", "chartjs-2", "dist", "Chart.min.js")    // <- ubicación anterior
    };
            var chartPath = chartCandidates.FirstOrDefault(System.IO.File.Exists);

            // Logo como data URI
            if (System.IO.File.Exists(logoPath))
            {
                var bytes = await System.IO.File.ReadAllBytesAsync(logoPath);
                ViewBag.LogoDataUri = "data:image/png;base64," + Convert.ToBase64String(bytes);
            }
            else
            {
                ViewBag.LogoDataUri = null;
            }

            // Chart.js embebido en línea (solo si se encontró)
            ViewBag.ChartInline = chartPath != null
                ? await System.IO.File.ReadAllTextAsync(chartPath, Encoding.UTF8)
                : null;

            var switches = string.Join(" ",
                "--enable-javascript",
                "--no-stop-slow-scripts",
                "--javascript-delay 2200",
                "--viewport-size 1280x1024",
                "--dpi 180",
                "--print-media-type",
                // === Footer ===
                "--footer-center \"Página [page] de [toPage]\"",
                "--footer-font-size 9",
                "--footer-spacing 4"
            );

            return new Rotativa.AspNetCore.ViewAsPdf("PagosIngresosPdf", vm)
            {
                PageSize = Rotativa.AspNetCore.Options.Size.A4,
                PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,
                CustomSwitches = switches
            };
        }





        // ReportesController.cs  (método PagosIngresosPdf)
        //[HttpGet("PagosIngresosPdf")]
        //public async Task<IActionResult> PagosIngresosPdf(
        //    DateTime? desde, DateTime? hasta, short? tipoPagoId, short? formaPagoId, short? plataformaId)
        //{
        //    var vm = await BuildReporteIngresos(desde, hasta, tipoPagoId, formaPagoId, plataformaId);

        //    if (_env == null) throw new InvalidOperationException("IWebHostEnvironment no disponible.");
        //    var webRoot = _env.WebRootPath;                // ...\wwwroot

        //    // Rutas locales
        //    var logoPath = Path.Combine(webRoot, "img", "login", "logo-oasis.png");
        //    var chartPath = Path.Combine(webRoot, "lib", "chartjs-2", "dist", "Chart.min.js"); // <— v2.9.4 ES5

        //    // file:///... para wkhtmltopdf + verificación de existencia
        //    string FileUrl(string p) => "file:///" + p.Replace("\\", "/");
        //    ViewBag.LogoUrl = System.IO.File.Exists(logoPath) ? FileUrl(logoPath) : "";
        //    //ViewBag.ChartJs = System.IO.File.Exists(chartPath) ? FileUrl(chartPath) : "";
        //    ViewBag.ChartJs = "file:///" + chartPath.Replace("\\", "/");

        //    // switches pensados para JS + archivos locales
        //    var switches = string.Join(" ",
        //        "--enable-local-file-access",
        //        $"--allow \"{webRoot}\"",                // <— permite leer wwwroot
        //        "--enable-javascript",
        //        "--no-stop-slow-scripts",
        //        "--window-status ready",                // esperamos a window.status='ready'
        //        "--javascript-delay 3000",              // margen extra para pintar/convertir
        //        "--viewport-size 1280x1024",
        //        "--load-error-handling ignore",
        //        "--dpi 180"
        //    );

        //    return new Rotativa.AspNetCore.ViewAsPdf("PagosIngresosPdf", vm)
        //    {
        //        PageSize = Rotativa.AspNetCore.Options.Size.A4,
        //        PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,
        //        CustomSwitches = switches
        //    };
        //}

        // ReportesController.cs (método PagosIngresosPdf)
        //[HttpGet("PagosIngresosPdf")]
        //public async Task<IActionResult> PagosIngresosPdf(
        //    DateTime? desde, DateTime? hasta, short? tipoPagoId, short? formaPagoId, short? plataformaId)
        //{
        //    var vm = await BuildReporteIngresos(desde, hasta, tipoPagoId, formaPagoId, plataformaId);

        //    var webRoot = _env.WebRootPath;

        //    var logoPath = Path.Combine(webRoot, "img", "login", "logo-oasis.png");
        //    var chartPath = Path.Combine(webRoot, "lib", "chartjs-2", "Chart.min.js"); // <-- v2.9.4 ES5

        //    ViewBag.LogoUrl = System.IO.File.Exists(logoPath) ? FileUrl(logoPath) : "";
        //    ViewBag.ChartJs = System.IO.File.Exists(chartPath) ? FileUrl(chartPath) : "";

        //    var switches = string.Join(" ",
        //        "--enable-local-file-access",
        //        "--enable-javascript",
        //        "--no-stop-slow-scripts",
        //        "--window-status ready",
        //        "--javascript-delay 2000",
        //        "--viewport-size 1280x1024",
        //        "--load-error-handling ignore",
        //        "--dpi 180"
        //    );

        //    return new Rotativa.AspNetCore.ViewAsPdf("PagosIngresosPdf", vm)
        //    {
        //        PageSize = Rotativa.AspNetCore.Options.Size.A4,
        //        PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,
        //        CustomSwitches = switches
        //    };
        //}


        //[HttpGet("PagosIngresosPdf")]
        //public async Task<IActionResult> PagosIngresosPdf(
        //DateTime? desde, DateTime? hasta, short? tipoPagoId, short? formaPagoId, short? plataformaId)
        //{
        //    var vm = await BuildReporteIngresos(desde, hasta, tipoPagoId, formaPagoId, plataformaId);

        //    // Rutas locales (evita HTTP hacia tu propio sitio)
        //    var webRoot = _env.WebRootPath; // …/wwwroot
        //    ViewBag.LogoUrl = FileUrl(webRoot, "img", "login", "logo-oasis.png");
        //    ViewBag.ChartJs = FileUrl(webRoot, "lib", "chart.js", "dist", "chart.umd.min.js"); // <— sin “dist”

        //    // IMPORTANTES para JS y archivos locales
        //    var switches = string.Join(" ",
        //        "--enable-javascript",
        //        "--no-stop-slow-scripts",
        //        "--window-status ready",
        //        "--viewport-size 1280x1024",
        //        "--load-error-handling ignore",
        //        "--dpi 180",
        //        "--enable-local-file-access",   // <— necesario para file:///
        //        "--javascript-delay 400"        // pequeña espera extra
        //    );

        //    return new ViewAsPdf("PagosIngresosPdf", vm)
        //    {
        //        PageSize = Size.A4,
        //        PageOrientation = Orientation.Portrait,
        //        CustomSwitches = switches
        //    };
        //}


        //    [HttpGet("PagosIngresosPdf")]
        //    public async Task<IActionResult> PagosIngresosPdf(
        //DateTime? desde, DateTime? hasta, short? tipoPagoId, short? formaPagoId, short? plataformaId)
        //    {
        //        var vm = await BuildReporteIngresos(desde, hasta, tipoPagoId, formaPagoId, plataformaId);

        //        // URLs absolutas para wkhtmltopdf
        //        ViewBag.BaseUrl = $"{Request.Scheme}://{Request.Host}";
        //        ViewBag.LogoUrl = ViewBag.BaseUrl + Url.Content("~/img/login/logo-oasis.png");
        //        ViewBag.ChartJs = ViewBag.BaseUrl + Url.Content("~/lib/chart.js/dist/chart.umd.js");

        //        // IMPORTANTE: habilitar JS y esperar la señal "ready"
        //        var switches = string.Join(" ",
        //            "--enable-javascript",
        //            "--no-stop-slow-scripts",
        //            "--window-status ready",                 // esperamos a window.status='ready'
        //            "--viewport-size 1280x1024",             // da un viewport realista para Chart.js
        //            "--load-error-handling ignore",
        //            "--dpi 180"
        //        // ,"--javascript-delay 300"              // opcional, si quieres una mínima espera adicional
        //        );

        //        return new Rotativa.AspNetCore.ViewAsPdf("PagosIngresosPdf", vm)
        //        {
        //            PageSize = Rotativa.AspNetCore.Options.Size.A4,
        //            PageOrientation = Rotativa.AspNetCore.Options.Orientation.Portrait,
        //            CustomSwitches = switches
        //        };
        //    }

        //[HttpGet("PagosIngresosPdf")]
        //public async Task<IActionResult> PagosIngresosPdf(DateTime? desde, DateTime? hasta, short? tipoPagoId, short? formaPagoId, short? plataformaId)
        //{
        //    var vm = await BuildReporteIngresos(desde, hasta, tipoPagoId, formaPagoId, plataformaId);

        //    ViewBag.BaseUrl = $"{Request.Scheme}://{Request.Host}";
        //    ViewBag.LogoUrl = ViewBag.BaseUrl + Url.Content("~/img/login/logo-oasis.png");

        //    return new ViewAsPdf("PagosIngresosPdf", vm)
        //    {
        //        PageSize = Size.A4,
        //        PageOrientation = Orientation.Portrait,
        //        CustomSwitches = "--dpi 180"
        //    };
        //}


        // ====== FACTOR COMÚN (arma el VM para HTML y PDF) ======
        private async Task<ReporteReservasVM> BuildReporteReservas(DateTime? desde, DateTime? hasta, int? estadoId, bool modoPorPagos)
        {
            var vm = new ReporteReservasVM
            {
                Desde = desde ?? DateTime.Today,
                Hasta = hasta ?? DateTime.Today,
                EstadoId = estadoId,
                ModoPorPagos = modoPorPagos
            };

            var d = vm.Desde!.Value.Date;
            var h = vm.Hasta!.Value.Date.AddDays(1); // exclusivo

            // === BASE: reservas que se solapan con el rango (por estancia)
            var qRes = _db.reservas
                .Include(r => r.Cliente).ThenInclude(c => c.Persona)
                .Include(r => r.Estado)
                .Include(r => r.Detalles).ThenInclude(dtl => dtl.Habitacion).ThenInclude(hb => hb.TipoHabitacion)
                .Include(r => r.Pagos)
                .AsNoTracking()
                .Where(r => !(r.FechaCheckOut <= d || r.FechaCheckIn >= h));

            if (estadoId.HasValue)
                qRes = qRes.Where(r => r.EstadoReservaID == estadoId.Value);

            var reservas = await qRes.ToListAsync();

            // KPIs rápidos
            int NochesEnRango(DateTime ci, DateTime co)
            {
                var ini = ci < d ? d : ci;
                var fin = co > h ? h : co;
                return Math.Max(0, (fin - ini).Days);
            }

            var nochesOcupadas = reservas.Sum(r => r.Detalles.Sum(_ => NochesEnRango(r.FechaCheckIn, r.FechaCheckOut)));
            var habitacionesActivas = await _db.habitaciones.CountAsync(x => x.Estado);
            var nochesDisponibles = habitacionesActivas * Math.Max(0, (h - d).Days);
            var roomRevenue = reservas.Sum(r => r.Detalles.Sum(x => x.TotalLinea)); // sin impuestos

            vm.Ocupacion = nochesDisponibles == 0 ? 0 : (decimal)nochesOcupadas / nochesDisponibles;
            vm.ADR = nochesOcupadas == 0 ? 0 : (nochesOcupadas == 0 ? 0 : roomRevenue / nochesOcupadas);
            vm.RevPAR = vm.ADR * vm.Ocupacion;

            vm.LlegadasHoy = reservas.Count(r => r.FechaCheckIn.Date == DateTime.Today);
            vm.SalidasHoy = reservas.Count(r => r.FechaCheckOut.Date == DateTime.Today);

            // Listado de reservas (transaccional)
            vm.Reservas = reservas.Select(r =>
            {
                var pagado = r.Pagos.Sum(p => p.MontoPagado);
                var hab = r.Detalles
                    .Select(d => $"{d.Habitacion.TipoHabitacion.Nombre} #{d.Habitacion.NumeroHabitacion}")
                    .FirstOrDefault() ?? "—";
                var cliente = $"{r.Cliente.Persona.PrimerNombre} {r.Cliente.Persona.PrimerApellido}".Trim();
                return new ReservaRow(
                    r.Codigo, r.Estado.Nombre, r.FechaCheckIn, r.FechaCheckOut,
                    cliente, hab, r.Total, pagado, r.Total - pagado
                );
            }).OrderByDescending(x => x.In).ToList();

            // === Pagos (modo por pagos)
            var qPagos = _db.pagosReserva
                .Include(p => p.TipoPago)
                .Include(p => p.Plataforma)
                .Include(p => p.Reserva)
                .AsNoTracking()
                .Where(p => p.FechaPago.Date >= d && p.FechaPago.Date < h);

            var pagos = await qPagos.ToListAsync();

            vm.TotalCobrado = pagos.Sum(p => p.MontoPagado);
            
            vm.Pagos = pagos
            .OrderByDescending(p => p.FechaPago)
            .Select(p => new PagoRowRes(
                p.FechaPago,
                p.TipoPago?.Nombre ?? "—",
                p.Plataforma?.Nombre,
                p.MontoPagado,
                p.Reserva.Codigo))
            .ToList();


            //vm.Pagos = pagos
            //    .OrderByDescending(p => p.FechaPago)
            //    .Select(p => new PagoRoww(
            //        p.FechaPago, p.TipoPago?.Nombre ?? "—", p.Plataforma?.Nombre, p.MontoPagado, p.Reserva.Codigo))
            //    .ToList();

            vm.PorTipoPago = pagos
                .GroupBy(p => p.TipoPago?.Nombre ?? "—")
                .Select(g => new ItemMonto(g.Key, g.Sum(x => x.MontoPagado)))
                .ToList();

            vm.PorPlataforma = pagos
                .GroupBy(p => p.Plataforma?.Nombre ?? "—")
                .Select(g => new ItemMonto(g.Key, g.Sum(x => x.MontoPagado)))
                .Where(x => x.Monto > 0)                      // 👈 filtra ceros
                .OrderByDescending(x => x.Monto)
                .ToList();

            // Cuentas por cobrar (reservas del rango con pendiente > 0)
            vm.CuentasPorCobrar = vm.Reservas.Where(r => r.Pendiente > 0).Sum(r => r.Pendiente);

            return vm;
        }

        // ====== HTML ======
        [HttpGet("Reservas")]
        public async Task<IActionResult> Reservas(DateTime? desde, DateTime? hasta, int? estadoId, bool modoPorPagos = false)
        {
            var vm = await BuildReporteReservas(desde, hasta, estadoId, modoPorPagos);
            return View(vm);
        }

        // ====== PDF (Rotativa) ======
        [HttpGet("ReservasPdf")]
        public async Task<IActionResult> ReservasPdf(DateTime? desde, DateTime? hasta, int? estadoId, bool modoPorPagos = false)
        {
            var vm = await BuildReporteReservas(desde, hasta, estadoId, modoPorPagos);

            // Logo absoluto para wkhtmltopdf
            ViewBag.BaseUrl = $"{Request.Scheme}://{Request.Host}";
            ViewBag.LogoUrl = ViewBag.BaseUrl + Url.Content("~/img/login/logo-oasis.png");

            return new ViewAsPdf("ReservasPdf", vm)
            {
                PageSize = Size.A4,
                PageOrientation = Orientation.Portrait,
                CustomSwitches = "--dpi 180"
            };
        }
    }
}
