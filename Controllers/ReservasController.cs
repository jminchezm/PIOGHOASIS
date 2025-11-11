using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using PIOGHOASIS.Filters;
using PIOGHOASIS.Infraestructure.Data;
using PIOGHOASIS.Models;
using PIOGHOASIS.Models.ViewModels;
using PIOGHOASIS.Services;
using Rotativa.AspNetCore;
using Rotativa.AspNetCore.Options;
using System;
using System.Security.Claims;

namespace PIOGHOASIS.Controllers
{
    [RequireCajaAbierta]
    [Route("Reservas")]
    public class ReservasController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IReservaPricingService _pricing;

        const string KEY = "RESERVA_TMP";

        public ReservasController(AppDbContext db, IReservaPricingService pricing)
        { _db = db; _pricing = pricing; }

        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index(string? codigo, string? cliente, DateTime? desde, DateTime? hasta, string? estado)
        {
            // Estados disponibles desde BD
            var estadosDb = await _db.estadosReserva.AsNoTracking().ToListAsync();
            ViewBag.Estados = estadosDb.Select(e => e.Nombre).Union(new[] { "Todos" }).Distinct().ToList();

            // Por defecto “Reservada”
            var estadoFiltro = string.IsNullOrWhiteSpace(estado) ? "Reservada" : estado!.Trim();

            // Mapear nombre/código a ID (si viene "Todos" no filtra)
            int? estadoId = null;
            if (!string.Equals(estadoFiltro, "Todos", StringComparison.OrdinalIgnoreCase))
            {
                string Norm(string s) => (s ?? "").Trim().ToUpperInvariant();
                estadoId = estadosDb
                    .FirstOrDefault(e => Norm(e.Nombre) == Norm(estadoFiltro) || Norm(e.Codigo) == Norm(estadoFiltro))
                    ?.EstadoReservaID;

                // Si no lo encontró y además no enviaron nada, intentamos dejar 'Reservada' por defecto
                if (estadoId == null && string.IsNullOrWhiteSpace(estado))
                {
                    estadoId = estadosDb.FirstOrDefault(e => Norm(e.Nombre) == "RESERVADA")?.EstadoReservaID;
                    estadoFiltro = "Reservada";
                }
            }

            //var q = _db.reservas
            //    .Include(r => r.Cliente).ThenInclude(c => c.Persona)
            //    .Include(r => r.Estado)
            //    .AsNoTracking()
            //    .AsQueryable();

            var q = _db.reservas
            .Include(r => r.Cliente).ThenInclude(c => c.Persona)
            .Include(r => r.Estado)
            .Include(r => r.Detalles)                           // +++
                .ThenInclude(d => d.Habitacion)                // +++
                    .ThenInclude(h => h.TipoHabitacion)        // +++
            .AsNoTracking()
            .AsQueryable();

            if (!string.IsNullOrWhiteSpace(codigo))
                q = q.Where(r => r.Codigo.Contains(codigo));

            if (!string.IsNullOrWhiteSpace(cliente))
            {
                var term = cliente.Trim();
                q = q.Where(r =>
                    (r.Cliente.Persona.PrimerNombre + " " +
                     (r.Cliente.Persona.SegundoNombre ?? "") + " " +
                     r.Cliente.Persona.PrimerApellido + " " +
                     (r.Cliente.Persona.SegundoApellido ?? "")
                    ).Contains(term));
            }

            if (!desde.HasValue && !hasta.HasValue)
                desde = DateTime.Today;

            // Rango de fechas – se considera SOLAPAMIENTO con la estancia
            if (desde.HasValue || hasta.HasValue)
            {
                var d = (desde ?? DateTime.MinValue).Date;
                var h = (hasta ?? DateTime.MaxValue).Date;
                q = q.Where(r => !(r.FechaCheckOut < d || r.FechaCheckIn > h));
            }

            if (estadoId.HasValue)
                q = q.Where(r => r.EstadoReservaID == estadoId.Value);

            var model = await q.OrderByDescending(r => r.ReservaID)
                               .Take(400)
                               .ToListAsync();

            return IsAjax ? PartialView(nameof(Index), model) : View(model);
        }

        // Helper para detectar peticiones AJAX
        private bool IsAjax => string.Equals(
            Request.Headers["X-Requested-With"], "XMLHttpRequest",
            StringComparison.OrdinalIgnoreCase);



        //Paso 1: Elegir habitación + fechas
        // Paso 1: Elegir habitación + fechas
        [HttpGet("ElegirHabitacion")]
        public async Task<IActionResult> ElegirHabitacion(DateTime? checkIn, DateTime? checkOut, int personas = 1)
        {
            // ViewModel para la barra naranja
            var vm = new BusquedaHabitacionVM
            {
                CheckIn = checkIn,
                CheckOut = checkOut,
                Personas = personas
            };

            // 1) Siempre: si hay resumen en sesión, lo pintamos en el sidebar
            var resumen = GetResumen();
            if (resumen != null)
            {
                ViewData["ResumenSidebar"] =
                    await this.RenderViewAsync("_ResumenReservaSidebar", resumen, partial: true);
            }

            // 2) Si no hay fechas válidas, solo mostramos barra + resumen
            if (checkIn == null || checkOut == null || checkOut <= checkIn)
                return View(vm);

            // 3) Si hay fechas, calculamos disponibilidad (misma lógica que en Buscar)
            var (lista, habsSinTarifa) =
                await BuscarDisponibilidadInterna(checkIn.Value.Date, checkOut.Value.Date, personas);

            ViewBag.HabitacionesSinTarifa = habsSinTarifa;

            // Metemos el partial de resultados en ViewData para que la vista lo coloque
            ViewData["Resultados"] =
                await this.RenderViewAsync("_ResultadosHabitaciones", lista, partial: true);

            return View(vm);
        }

        private async Task<(List<HabitacionDisponibleVM> res, List<string> habsSinTarifa)>
    BuscarDisponibilidadInterna(DateTime ci, DateTime co, int personas)
        {
            var res = new List<HabitacionDisponibleVM>();
            var habsSinTarifa = new List<string>();

            var codigosBloqueo = new[] { "ESTHAB0001", "ESTHAB0002" }; // Reservada, Confirmada (ajusta a tus códigos)
            var idsBloqueo = await _db.estadosReserva
                .Where(e => codigosBloqueo.Contains(e.Codigo))
                .Select(e => e.EstadoReservaID)
                .ToListAsync();

            // 2) Fallback por nombre si no hay coincidencias por código
            if (!idsBloqueo.Any())
            {
                var nombres = new[] { "RESERVADA", "CONFIRMADA" };
                idsBloqueo = await _db.estadosReserva
                    .Where(e => nombres.Any(n => e.Nombre.ToUpper().Contains(n)))
                    .Select(e => e.EstadoReservaID)
                    .ToListAsync();
            }

            // 3) Habitaciones disponibles (sin solape con reservas en estados bloqueantes)
            var habsDisponibles = await _db.habitaciones
                .Where(h => h.Estado)
                .Include(h => h.TipoHabitacion)
                .Where(h => !_db.detalleReservas.Any(d =>
                    d.HabitacionID == h.HabitacionID &&
                    idsBloqueo.Contains(d.Reserva.EstadoReservaID) &&
                    !(d.Reserva.FechaCheckOut <= ci || d.Reserva.FechaCheckIn >= co)))
                .AsNoTracking()
                .ToListAsync();

            res = new List<HabitacionDisponibleVM>();
            habsSinTarifa = new List<string>(); // para mensaje interno

            foreach (var h in habsDisponibles)
            {
                var cap = Math.Max(1, (int)(h.CapacidadPersonas ?? 1));
                var defaultPersonas = Math.Clamp(personas, 1, cap);

                var item = new HabitacionDisponibleVM
                {
                    HabitacionID = h.HabitacionID,
                    Codigo = h.Codigo,
                    Imagen = string.IsNullOrWhiteSpace(h.Imagen) ? "/img/DefaultHabitacion.png" : h.Imagen,
                    Titulo = $"{h.TipoHabitacion?.Nombre} #{h.NumeroHabitacion}",
                    TipoNombre = h.TipoHabitacion?.Nombre ?? "",
                    NumeroHabitacion = h.NumeroHabitacion,
                    Capacidad = cap,
                    PersonasSeleccionadas = defaultPersonas
                };

                // Tarifas por # de personas
                for (int p = 1; p <= cap; p++)
                {
                    var tarifasDb = await _db.tarifasHabitacion.AsNoTracking()
                        .Where(t => t.HabitacionID == h.HabitacionID
                                    && t.NumeroPersonas == p
                                    && t.FechaInicio <= ci
                                    && t.FechaFin >= co.AddDays(-1))
                        .OrderBy(t => t.PrecioNoche)
                        .ToListAsync();

                    if (tarifasDb.Count == 0)
                    {
                        tarifasDb = await _db.tarifasHabitacion.AsNoTracking()
                            .Where(t => t.HabitacionID == h.HabitacionID
                                        && t.NumeroPersonas == p
                                        && !(t.FechaFin < ci || t.FechaInicio > co.AddDays(-1)))
                            .OrderBy(t => t.PrecioNoche)
                            .ToListAsync();
                    }

                    if (tarifasDb.Count > 0)
                    {
                        foreach (var t in tarifasDb)
                        {
                            // precio final por noche CON impuestos, tal cual viene de BD
                            var precioVenta = decimal.Round(t.PrecioNoche, 2, MidpointRounding.AwayFromZero);

                            // solo para obtener base/impuestos
                            var (baseNeta, inguat, iva, _) = ReservaPricingService.DesglosarDesdeTotal(precioVenta, 1);

                            item.Tarifas.Add(new TarifaOpcionVM
                            {
                                Personas = p,
                                TarifaID = t.TarifaID,

                                // Lo que se muestra en la tarjeta: EXACTO a lo que grabaste
                                PrecioNoche = precioVenta,
                                TotalConImpuestos = precioVenta, // 1 noche

                                BaseSinImpuestos = baseNeta,
                                Inguat = inguat,
                                Iva = iva,

                                Etiqueta = string.IsNullOrWhiteSpace(t.EtiquetaTemporada)
                                           ? $"Tarifa {p} persona(s)"
                                           : t.EtiquetaTemporada
                            });
                        }
                    }
                    else
                    {
                        var (precioConImpuestos, tarifaId) =
                            await _pricing.PrecioPorNoche(h.HabitacionID, p, ci, co);

                        if (precioConImpuestos > 0)
                        {
                            var precioVenta = decimal.Round(precioConImpuestos, 2, MidpointRounding.AwayFromZero);
                            var (baseNeta, inguat, iva, _) = ReservaPricingService.DesglosarDesdeTotal(precioVenta, 1);

                            item.Tarifas.Add(new TarifaOpcionVM
                            {
                                Personas = p,
                                TarifaID = tarifaId,

                                PrecioNoche = precioVenta,
                                TotalConImpuestos = precioVenta,

                                BaseSinImpuestos = baseNeta,
                                Inguat = inguat,
                                Iva = iva,

                                Etiqueta = "Tarifa estándar"
                            });
                        }
                    }
                }

                // ========= BLOQUE: personas con tarifa disponible =========

                var personasDisponibles = item.Tarifas
                    .Select(t => t.Personas)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();

                // Si no hay ninguna tarifa válida para esta habitación, ni la mostramos
                if (!personasDisponibles.Any())
                {
                    habsSinTarifa.Add($"{h.TipoHabitacion?.Nombre} #{h.NumeroHabitacion}");
                    continue;
                }

                // *** NUEVO: filtrar por la ocupación digitada ***
                // Solo queremos habitaciones que tengan tarifa para "personas"
                if (personas > 0 && !personasDisponibles.Contains(personas))
                {
                    // opcionalmente podrías guardar aquí para otro mensaje
                    continue;
                }

                item.PersonasDisponibles = personasDisponibles;

                // Personas seleccionadas: usamos la cantidad digitada (ya sabemos que existe tarifa para ella)
                var personasSeleccionadas = personas > 0 ? personas : item.PersonasSeleccionadas;

                if (!personasDisponibles.Contains(personasSeleccionadas))
                    personasSeleccionadas = personasDisponibles.First();

                item.PersonasSeleccionadas = personasSeleccionadas;

                // Tarifa activa según PersonasSeleccionadas
                var active = item.Tarifas
                    .FirstOrDefault(t => t.Personas == personasSeleccionadas)
                    ?? item.Tarifas.FirstOrDefault();

                if (active != null)
                {
                    item.TarifaSeleccionadaID = active.TarifaID;
                    item.PrecioNoche = active.PrecioNoche;
                    item.TotalConImpuestos = active.TotalConImpuestos;
                }

                // ================================================================

                res.Add(item);
            }

            return (res, habsSinTarifa);
        }



        //[HttpGet("ElegirHabitacion")]
        //public IActionResult ElegirHabitacion() => View(new BusquedaHabitacionVM());


        [HttpGet("Buscar")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Buscar(DateTime? checkIn, DateTime? checkOut, int personas = 1)
        {
            if (checkIn == null || checkOut == null || checkOut <= checkIn)
            {
                var vacio = new List<HabitacionDisponibleVM>();
                return PartialView("_ResultadosHabitaciones", vacio);
            }

            var ci = checkIn.Value.Date;
            var co = checkOut.Value.Date;

            var (res, habsSinTarifa) = await BuscarDisponibilidadInterna(ci, co, personas);
            ViewBag.HabitacionesSinTarifa = habsSinTarifa;

            // Buscar siempre devuelve SOLO el partial (lo inyecta tu JS en #resultados)
            return PartialView("_ResultadosHabitaciones", res);
        }


        // Selecciona 1 habitación y pasa a resumen

        [HttpPost("Seleccionar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Seleccionar(int habitacionId,
                                            DateTime checkIn,
                                            DateTime checkOut,
                                            int personas,
                                            int? tarifaId)
        {
            var h = await _db.habitaciones
                .Include(x => x.TipoHabitacion)
                .FirstOrDefaultAsync(x => x.HabitacionID == habitacionId);

            if (h == null) return NotFound();

            // Crear / reutilizar resumen en sesión
            var resumen = GetResumen();
            if (resumen == null)
            {
                resumen = new ReservaResumenVM
                {
                    CheckIn = checkIn.Date,
                    CheckOut = checkOut.Date
                };
            }
            else
            {
                // Opcional: forzar mismas fechas para todas las habitaciones
                resumen.CheckIn = checkIn.Date;
                resumen.CheckOut = checkOut.Date;
            }

            var cap = Math.Max(1, (int)(h.CapacidadPersonas ?? 1));
            personas = Math.Clamp(personas, 1, cap);

            decimal precio;
            int? tarifaUsada;

            if (tarifaId.HasValue)
            {
                var t = await _db.tarifasHabitacion.FirstOrDefaultAsync(x =>
                    x.TarifaID == tarifaId.Value &&
                    x.HabitacionID == habitacionId &&
                    x.NumeroPersonas == personas &&
                    x.FechaInicio <= checkIn.Date &&
                    x.FechaFin >= checkOut.Date.AddDays(-1));

                if (t == null)
                {
                    var r = await _pricing.PrecioPorNoche(habitacionId, personas, checkIn, checkOut);
                    precio = r.precio;
                    tarifaUsada = r.tarifaId;
                }
                else
                {
                    precio = t.PrecioNoche;      // ya con impuestos
                    tarifaUsada = t.TarifaID;
                }
            }
            else
            {
                var r = await _pricing.PrecioPorNoche(habitacionId, personas, checkIn, checkOut);
                precio = r.precio;               // ya con impuestos
                tarifaUsada = r.tarifaId;
            }

            var noches = (int)(checkOut.Date - checkIn.Date).TotalDays;
            var (baseNeta, inguat, iva, total) =
                ReservaPricingService.DesglosarDesdeTotal(precio, noches);

            // Buscar si ya existe línea para esta habitación
            var linea = resumen.Lineas.FirstOrDefault(l => l.HabitacionID == habitacionId);
            if (linea == null)
            {
                linea = new ReservaLineaVM
                {
                    HabitacionID = habitacionId,
                    HabitacionTitulo = $"{h.TipoHabitacion?.Nombre} #{h.NumeroHabitacion}"
                };
                resumen.Lineas.Add(linea);
            }

            linea.Personas = personas;
            linea.Noches = noches;
            linea.TarifaID = tarifaUsada;
            linea.PrecioNoche = precio;
            linea.PrecioNocheOriginal = precio;   // si luego quieres manejar descuentos, aquí puedes guardar lista
            linea.Subtotal = baseNeta;
            linea.Impuestos = inguat + iva;
            linea.Total = total;

            SaveResumen(resumen);

            // --- Respuesta ---
            if (IsAjax)
            {
                // Renderizar de nuevo el sidebar con todas las habitaciones seleccionadas
                var html = await this.RenderViewAsync("_ResumenReservaSidebar", resumen, partial: true);
                return Json(new { ok = true, resumenHtml = html });
            }

            // Navegación tradicional (por si alguien llama sin AJAX)
            return RedirectToAction(nameof(Cliente));
        }


        //[HttpPost("Seleccionar")]
        //public async Task<IActionResult> Seleccionar(int habitacionId, DateTime checkIn, DateTime checkOut, int personas, int? tarifaId)
        //{
        //    var h = await _db.habitaciones.Include(x => x.TipoHabitacion)
        //                                  .FirstOrDefaultAsync(x => x.HabitacionID == habitacionId);
        //    if (h == null) return NotFound();

        //    var cap = Math.Max(1, (int)(h.CapacidadPersonas ?? 1));
        //    personas = Math.Clamp(personas, 1, cap);

        //    decimal precio;
        //    int? tarifaUsada;

        //    if (tarifaId.HasValue)
        //    {
        //        // Validar la tarifa elegida
        //        var t = await _db.tarifasHabitacion.FirstOrDefaultAsync(x =>
        //            x.TarifaID == tarifaId.Value &&
        //            x.HabitacionID == habitacionId &&
        //            x.NumeroPersonas == personas &&
        //            // rango de fechas: [FechaInicio, FechaFin] cubre TODO el rango (ajusta a tu regla)
        //            x.FechaInicio <= checkIn.Date && x.FechaFin >= checkOut.Date.AddDays(-1));

        //        if (t == null)
        //        {
        //            // si la tarifa enviada no es válida, volvemos al pricing service
        //            var r = await _pricing.PrecioPorNoche(habitacionId, personas, checkIn, checkOut);
        //            precio = r.precio;
        //            tarifaUsada = r.tarifaId;
        //        }
        //        else
        //        {
        //            precio = t.PrecioNoche;
        //            tarifaUsada = t.TarifaID;
        //        }
        //    }
        //    else
        //    {
        //        var r = await _pricing.PrecioPorNoche(habitacionId, personas, checkIn, checkOut);
        //        precio = r.precio;
        //        tarifaUsada = r.tarifaId;
        //    }

        //    var noches = (int)(checkOut.Date - checkIn.Date).TotalDays;

        //    // precio = precio de venta por noche (con impuestos)
        //    var (baseNeta, inguat, iva, total) = ReservaPricingService.DesglosarDesdeTotal(precio, noches);

        //    var resumen = new ReservaResumenVM
        //    {
        //        CheckIn = checkIn.Date,
        //        CheckOut = checkOut.Date,
        //        Noches = noches,
        //        Personas = personas,
        //        HabitacionID = habitacionId,
        //        HabitacionTitulo = $"{h.TipoHabitacion?.Nombre} #{h.NumeroHabitacion}",

        //        // Precio de venta por noche (con impuestos)
        //        PrecioNoche = precio,
        //        TarifaID = tarifaUsada,

        //        // === NUEVO: guardar precio de lista ===
        //        PrecioNocheOriginal = precio,

        //        // Subtotal sin impuestos (todas las noches)
        //        Subtotal = baseNeta,

        //        // Impuestos totales (INGUAT + IVA de todas las noches)
        //        Impuestos = inguat + iva,

        //        // Total a pagar (con impuestos)
        //        Total = total
        //    };

        //    HttpContext.Session.SetString(KEY, System.Text.Json.JsonSerializer.Serialize(resumen));

        //    // Si viene por AJAX devolvemos una URL para redirigir al paso Cliente
        //    if (Request.Headers.TryGetValue("X-Requested-With", out var xh) &&
        //        string.Equals(xh.ToString(), "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
        //    {
        //        return Json(new { ok = true, redirectUrl = Url.Action(nameof(Cliente)) });
        //    }

        //    // Navegación normal
        //    return RedirectToAction(nameof(Cliente));
        //}


        // Paso 2: Cliente

        [HttpGet("Cliente")]
        public async Task<IActionResult> Cliente()
        {
            var resumen = GetResumen();
            if (resumen == null || !resumen.Lineas.Any())
                return RedirectToAction(nameof(ElegirHabitacion));

            var vm = new ReservaCreateVM
            {
                Resumen = resumen,
                Clientes = await _db.clientes
                    .Include(c => c.Persona)
                    .Where(c => c.Estado)
                    .AsNoTracking()
                    .ToListAsync()
            };

            ViewData["ResumenEditable"] = false;

            return View(vm);
        }

        //[HttpGet("Cliente")]
        //public async Task<IActionResult> Cliente()
        //{
        //    var resumen = GetResumen();
        //    if (resumen == null) return RedirectToAction(nameof(ElegirHabitacion));

        //    var vm = new ReservaCreateVM
        //    {
        //        Resumen = resumen,
        //        Clientes = await _db.clientes.Include(c => c.Persona).Where(c => c.Estado).AsNoTracking().ToListAsync()
        //    };
        //    return View(vm);
        //}

        [HttpPost("Cliente")]
        [ValidateAntiForgeryToken]
        public IActionResult Cliente(string clienteId)
        {
            var resumen = GetResumen();
            if (resumen == null)
                return Json(new { ok = false, redirectUrl = Url.Action(nameof(ElegirHabitacion)) });

            resumen.ClienteID = clienteId;
            var cli = _db.clientes.Include(c => c.Persona).FirstOrDefault(c => c.ClienteID == clienteId);
            resumen.ClienteNombre = $"{cli?.Persona?.PrimerNombre} {cli?.Persona?.PrimerApellido}".Trim();
            SaveResumen(resumen);

            // Si es AJAX, manda JSON para que tu JS haga la navegación dentro del host
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { ok = true, redirectUrl = Url.Action(nameof(Confirmar)) });

            // Navegación normal (si no es AJAX)
            return RedirectToAction(nameof(Confirmar));
        }

        [HttpGet("BuscarClientes")]
        public async Task<IActionResult> BuscarClientes(string? q, int top = 10)
        {
            q = (q ?? "").Trim();
            if (q.Length < 2)
                return Json(Array.Empty<object>());

            // Búsqueda por nombre o DPI (ajusta el nombre del campo del DPI si difiere)
            var qry = _db.clientes
                .Include(c => c.Persona)
                .AsNoTracking()
                .Where(c => c.Estado);

            // Campos típicos: PrimerNombre, SegundoNombre, PrimerApellido, SegundoApellido, DPI (ajústalo si se llama distinto)
            qry = qry.Where(c =>
                (c.Persona.PrimerNombre + " " + (c.Persona.SegundoNombre ?? "") + " " + c.Persona.PrimerApellido + " " + (c.Persona.SegundoApellido ?? "")).Contains(q) ||
                (c.Persona.NumeroDocumento ?? "").Contains(q) ||
                (c.ClienteID ?? "").Contains(q)
            );

            var list = await qry
                .OrderBy(c => c.Persona.PrimerNombre).ThenBy(c => c.Persona.PrimerApellido)
                .Take(top)
                .Select(c => new {
                    id = c.ClienteID,
                    nombre = (c.Persona.PrimerNombre + " " + (c.Persona.SegundoNombre ?? "") + " " + c.Persona.PrimerApellido + " " + (c.Persona.SegundoApellido ?? "")).Trim(),
                    dpi = c.Persona.NumeroDocumento,               // <-- si se llama distinto (NoDocumento, NIT, etc.), cámbialo aquí
                    telefono = c.Persona.Telefono1,     // opcional
                    correo = c.Persona.Email          // opcional
                })
                .ToListAsync();

            return Json(list);
        }


        // Paso 3: Confirmación
        [HttpGet("Confirmar")]
        public IActionResult Confirmar()
        {
            var resumen = GetResumen();
            if (resumen == null || resumen.ClienteID == null) return RedirectToAction(nameof(Cliente));
            ViewData["ResumenEditable"] = false;
            return View(resumen);
        }

        [HttpPost("Confirmar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarPost()
        {
            var r = GetResumen();

            if (r == null || r.ClienteID == null || !r.Lineas.Any())
            {
                if (IsAjax)
                    return Json(new { ok = false, msg = "Sesión de reserva perdida.", redirectUrl = Url.Action(nameof(ElegirHabitacion)) });

                return RedirectToAction(nameof(ElegirHabitacion));
            }

            // Verificación final de disponibilidad para cada habitación
            foreach (var l in r.Lineas)
            {
                if (!await _pricing.HabitacionDisponible(l.HabitacionID, r.CheckIn, r.CheckOut))
                {
                    var msg = $"La habitación {l.HabitacionTitulo} ya no está disponible.";
                    if (IsAjax)
                        return Json(new { ok = false, msg, redirectUrl = Url.Action(nameof(ElegirHabitacion)) });

                    TempData["msg"] = msg;
                    return RedirectToAction(nameof(ElegirHabitacion));
                }
            }

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var reserva = new Reserva
                {
                    ClienteID = r.ClienteID!,
                    UsuarioID = GetUserId(),
                    UsuarioFinaliza = null,
                    EstadoReservaID = await EstadoId("ESTHAB0001"), // Reservada
                    FechaCheckIn = r.CheckIn,
                    FechaCheckOut = r.CheckOut,
                    Subtotal = r.Subtotal,
                    Impuestos = r.Impuestos,
                    Total = r.Total,
                    Codigo = await NextCodigoAsync()
                };

                _db.reservas.Add(reserva);
                await _db.SaveChangesAsync();

                // Crear un DetalleReserva por habitación
                foreach (var l in r.Lineas)
                {
                    decimal? precioLista = null;
                    decimal? descPorNoche = null;

                    if (l.PrecioNocheOriginal > 0)
                    {
                        precioLista = l.PrecioNocheOriginal;
                        if (l.PrecioNocheOriginal > l.PrecioNoche)
                            descPorNoche = l.PrecioNocheOriginal - l.PrecioNoche;
                    }

                    var det = new DetalleReserva
                    {
                        ReservaID = reserva.ReservaID,
                        HabitacionID = l.HabitacionID,
                        Personas = l.Personas,
                        Noches = l.Noches,
                        PrecioPorNoche = l.PrecioNoche,
                        TotalLinea = l.Total,              // ya es precio * noches
                        TarifaID = l.TarifaID,
                        PrecioListaPorNoche = precioLista,
                        DescuentoPorNoche = descPorNoche
                    };

                    _db.detalleReservas.Add(det);
                }

                await _db.SaveChangesAsync();
                await tx.CommitAsync();

                HttpContext.Session.Remove(KEY);

                var url = Url.Action(nameof(Detalles), new { id = reserva.ReservaID });

                if (IsAjax)
                    return Json(new { ok = true, redirectUrl = url });

                return RedirectToAction(nameof(Detalles), new { id = reserva.ReservaID });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();

                if (IsAjax)
                    return StatusCode(500, new { ok = false, msg = "Error al confirmar.", detail = ex.Message });

                throw;
            }
        }


        //[HttpPost("Confirmar")]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> ConfirmarPost(decimal precioNoche)
        //{
        //    var r = GetResumen();
        //    if (r == null || r.ClienteID == null)
        //    {
        //        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        //            return Json(new { ok = false, msg = "Sesión de reserva perdida.", redirectUrl = Url.Action(nameof(ElegirHabitacion)) });

        //        return RedirectToAction(nameof(ElegirHabitacion));
        //    }

        //    // 1) Normalizar precio enviado
        //    if (precioNoche <= 0)
        //        precioNoche = r.PrecioNoche; // si viene algo raro, no lo cambio

        //    // 2) Si el usuario modificó el precio, recalculamos todo
        //    if (precioNoche != r.PrecioNoche)
        //    {
        //        // Guardar precio original si no estaba seteado (por seguridad)
        //        if (r.PrecioNocheOriginal <= 0)
        //            r.PrecioNocheOriginal = r.PrecioNoche;

        //        r.PrecioNoche = precioNoche;

        //        // precioNoche = precio de venta por noche (con impuestos)
        //        var (baseNeta, inguat, iva, total) = ReservaPricingService.DesglosarDesdeTotal(precioNoche, r.Noches);

        //        r.Subtotal = baseNeta;
        //        r.Impuestos = inguat + iva;
        //        r.Total = total;

        //        SaveResumen(r);  // vuelvo a dejar todo coherente en sesión
        //    }

        //    // 3) Verificación final de disponibilidad
        //    if (!await _pricing.HabitacionDisponible(r.HabitacionID, r.CheckIn, r.CheckOut))
        //    {
        //        var msg = "La habitación ya no está disponible.";
        //        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        //            return Json(new { ok = false, msg, redirectUrl = Url.Action(nameof(ElegirHabitacion)) });

        //        TempData["msg"] = msg;
        //        return RedirectToAction(nameof(ElegirHabitacion));
        //    }

        //    using var tx = await _db.Database.BeginTransactionAsync();
        //    try
        //    {
        //        var reserva = new Reserva
        //        {
        //            ClienteID = r.ClienteID!,
        //            UsuarioID = GetUserId(),
        //            UsuarioFinaliza = null,
        //            EstadoReservaID = await EstadoId("ESTHAB0001"), // "Reservada"
        //            FechaCheckIn = r.CheckIn,
        //            FechaCheckOut = r.CheckOut,
        //            Subtotal = r.Subtotal,
        //            Impuestos = r.Impuestos,
        //            Total = r.Total,
        //            Codigo = await NextCodigoAsync()
        //        };

        //        _db.reservas.Add(reserva);
        //        await _db.SaveChangesAsync();

        //        // ===== NUEVO: calcular precio de lista y descuento histórico =====
        //        decimal? precioLista = null;
        //        decimal? descPorNoche = null;

        //        // Si en el flujo se guardó el precio de lista en la sesión:
        //        if (r.PrecioNocheOriginal > 0)
        //        {
        //            precioLista = r.PrecioNocheOriginal;

        //            if (r.PrecioNocheOriginal > r.PrecioNoche)
        //                descPorNoche = r.PrecioNocheOriginal - r.PrecioNoche;
        //        }

        //        // Crear detalle con los nuevos campos
        //        var det = new DetalleReserva
        //        {
        //            ReservaID = reserva.ReservaID,
        //            HabitacionID = r.HabitacionID,
        //            Personas = r.Personas,
        //            Noches = r.Noches,

        //            // Precio realmente cobrado
        //            PrecioPorNoche = r.PrecioNoche,
        //            TotalLinea = r.PrecioNoche * r.Noches,
        //            TarifaID = r.TarifaID,

        //            // Históricos
        //            PrecioListaPorNoche = precioLista,
        //            DescuentoPorNoche = descPorNoche
        //        };

        //        _db.detalleReservas.Add(det);
        //        await _db.SaveChangesAsync();

        //        await tx.CommitAsync();
        //        HttpContext.Session.Remove(KEY);

        //        var url = Url.Action(nameof(Detalles), new { id = reserva.ReservaID });

        //        // Si viene por AJAX devolvemos JSON
        //        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        //            return Json(new { ok = true, redirectUrl = url });

        //        // Navegación tradicional
        //        return RedirectToAction(nameof(Detalles), new { id = reserva.ReservaID });
        //    }
        //    catch (Exception ex)
        //    {
        //        await tx.RollbackAsync();

        //        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        //            return StatusCode(500, new { ok = false, msg = "Error al confirmar.", detail = ex.Message });

        //        throw;
        //    }
        //}


        [HttpGet("Detalles/{id:int}")]
        public async Task<IActionResult> Detalles(int id)
        {
            var r = await _db.reservas
                .Include(x => x.Cliente).ThenInclude(c => c.Persona)
                .Include(x => x.Estado)
                .Include(x => x.Detalles).ThenInclude(d => d.Habitacion).ThenInclude(h => h.TipoHabitacion)
                .FirstOrDefaultAsync(x => x.ReservaID == id);
            if (r == null) return NotFound();
            return View(r);
        }

        // Helpers
        private ReservaResumenVM? GetResumen()
        {
            var str = HttpContext.Session.GetString(KEY);
            return string.IsNullOrWhiteSpace(str) ? null :
                System.Text.Json.JsonSerializer.Deserialize<ReservaResumenVM>(str);
        }
        private void SaveResumen(ReservaResumenVM r)
            => HttpContext.Session.SetString(KEY, System.Text.Json.JsonSerializer.Serialize(r));

        private async Task<int> EstadoId(string codigo)
            => await _db.estadosReserva.Where(e => e.Codigo == codigo).Select(e => e.EstadoReservaID).FirstAsync();

        private async Task<string> NextCodigoAsync()
        {
            var last = await _db.reservas.OrderByDescending(x => x.ReservaID).Select(x => x.Codigo).FirstOrDefaultAsync();
            var n = 0;
            if (!string.IsNullOrWhiteSpace(last))
            {
                var digits = new string(last.SkipWhile(c => !char.IsDigit(c)).ToArray());
                int.TryParse(digits, out n);
            }
            return $"RES{(n + 1).ToString("D7")}";
        }

        [HttpGet("ReservaPdf/{id:int}")]
        public async Task<IActionResult> ReservaPdf(int id, bool descargar = false)
        {
            var r = await _db.reservas
                .Include(x => x.Cliente).ThenInclude(c => c.Persona)
                .Include(x => x.Estado)
                .Include(x => x.Detalles).ThenInclude(d => d.Habitacion).ThenInclude(h => h.TipoHabitacion)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ReservaID == id);

            if (r == null) return NotFound();

            // === Cargar pagos de la reserva ===
            var pagos = await _db.pagosReserva
                .Include(p => p.FormaPago)
                .Include(p => p.TipoPago)
                .Include(p => p.Plataforma)
                .Where(p => p.ReservaID == id)
                .OrderBy(p => p.FechaPago)
                .AsNoTracking()
                .ToListAsync();

            var pagado = pagos.Sum(p => p.MontoPagado);
            var pendiente = Math.Max(0m, r.Total - pagado);

            var vm = new PIOGHOASIS.Models.ViewModels.PagoDetalleReservaVM
            {
                Reserva = r,
                Pagos = pagos,
                Pagado = pagado,
                Pendiente = pendiente
            };

            // ===== NUEVO: calcular precio de lista y descuento desde DETALLE_RESERVA =====
            decimal precioListaAcum = 0m;
            decimal precioFinalAcum = 0m;
            int nochesTotales = 0;

            if (r.Detalles != null && r.Detalles.Any())
            {
                foreach (var d in r.Detalles)
                {
                    nochesTotales += d.Noches;

                    // Precio final guardado
                    precioFinalAcum += d.PrecioPorNoche * d.Noches;

                    // Precio de lista histórico: si no existe, asumimos que lista == final (sin descuento)
                    var listaNoche = d.PrecioListaPorNoche ?? d.PrecioPorNoche;
                    precioListaAcum += listaNoche * d.Noches;
                }

                if (nochesTotales > 0 && precioListaAcum > precioFinalAcum)
                {
                    vm.PrecioListaPorNoche = precioListaAcum / nochesTotales;
                    vm.PrecioFinalPorNoche = precioFinalAcum / nochesTotales;
                    vm.DescuentoPorNoche = vm.PrecioListaPorNoche - vm.PrecioFinalPorNoche;
                    vm.DescuentoTotal = precioListaAcum - precioFinalAcum;
                }
            }
            // ===== FIN NUEVO =====

            var pdf = new ViewAsPdf("ReservaPdf", vm)   // <-- ahora el modelo ES PagoDetalleReservaVM
            {
                PageSize = Size.A4,
                PageOrientation = Orientation.Portrait,
                PageMargins = new Margins { Top = 18, Right = 14, Bottom = 16, Left = 14 },
                CustomSwitches = "--print-media-type --footer-center \"Página [page] de [toPage]\" --footer-font-size 8 --footer-spacing 5",
                ContentDisposition = descargar ? ContentDisposition.Attachment : ContentDisposition.Inline,
            };

            if (descargar)
                pdf.FileName = $"Reserva_{r.Codigo}.pdf";

            return pdf;
        }

        [HttpPost("Cancelar/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancelar(int id, string? motivo, string? estadoCodigo, string? estadoNombre)
        {
            var reserva = await _db.reservas.Include(r => r.Estado)
                                            .FirstOrDefaultAsync(r => r.ReservaID == id);
            if (reserva == null) return NotFound();

            var idCancelada = await ResolverEstadoId(codigo: estadoCodigo, nombre: estadoNombre, preferirCancelada: true);
            if (idCancelada == 0)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { ok = false, msg = "No se encontró un estado 'Cancelada'." });

                TempData["msg"] = "No se encontró un estado 'Cancelada'.";
                return RedirectToAction(nameof(Index));
            }

            if (reserva.EstadoReservaID != idCancelada)
            {
                reserva.EstadoReservaID = idCancelada;
                if (!string.IsNullOrWhiteSpace(motivo))
                {
                    reserva.NotaCancelacion = string.IsNullOrWhiteSpace(reserva.NotaCancelacion)
                        ? $"[CANCELADA] {DateTime.Now:yyyy-MM-dd HH:mm}: {motivo}"
                        : $"{reserva.NotaCancelacion}{Environment.NewLine}[CANCELADA] {DateTime.Now:yyyy-MM-dd HH:mm}: {motivo}";
                }
                await _db.SaveChangesAsync();
            }

            // ← clave: si es AJAX, devuelves una URL para el host
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { ok = true, redirectUrl = Url.Action(nameof(Index)) });

            // navegación tradicional
            return RedirectToAction(nameof(Index));
        }

        // Busca por código o nombre; si no viene nada, intenta heurística por nombre “Cancelada”
        private async Task<int> ResolverEstadoId(
            string? codigo = null,
            string? nombre = null,
            bool preferirCancelada = false)
        {
            var q = _db.estadosReserva.AsQueryable();

            if (!string.IsNullOrWhiteSpace(codigo))
                return await q.Where(e => e.Codigo == codigo)
                              .Select(e => e.EstadoReservaID)
                              .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(nombre))
                return await q.Where(e => e.Nombre.ToLower() == nombre.ToLower())
                              .Select(e => e.EstadoReservaID)
                              .FirstOrDefaultAsync();

            if (preferirCancelada)
            {
                // Heurística por nombre si no se proporcionó nada:
                var idByName = await q.Where(e =>
                            e.Nombre.ToLower().Contains("cancel"))
                        .Select(e => e.EstadoReservaID)
                        .FirstOrDefaultAsync();
                if (idByName != 0) return idByName;
            }

            return 0;
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

        //[HttpPost("RecalcularResumen")]
        //[ValidateAntiForgeryToken]
        //public IActionResult RecalcularResumen(decimal precioNoche)
        //{
        //    var r = GetResumen();
        //    if (r == null)
        //        return BadRequest("Sesión de reserva perdida.");

        //    // Normalizar
        //    if (precioNoche <= 0)
        //        precioNoche = r.PrecioNoche;

        //    // Asegurarnos de que hay precio original
        //    if (r.PrecioNocheOriginal <= 0)
        //        r.PrecioNocheOriginal = r.PrecioNoche;

        //    // Actualizar precio acordado
        //    r.PrecioNoche = precioNoche;

        //    // Recalcular totales con la MISMA lógica que en ConfirmarPost
        //    var (baseNeta, inguat, iva, total) = ReservaPricingService.DesglosarDesdeTotal(precioNoche, r.Noches);

        //    r.Subtotal = baseNeta;
        //    r.Impuestos = inguat + iva;
        //    r.Total = total;

        //    // Guardar en sesión para que todo siga consistente
        //    SaveResumen(r);

        //    // Devolver solo el resumen (sidebar) actualizado
        //    return PartialView("_ResumenReservaSidebar", r);
        //}

        [HttpPost("QuitarHabitacion")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> QuitarHabitacion(int habitacionId)
        {
            var resumen = GetResumen();
            if (resumen == null) return BadRequest("No hay reserva en sesión.");

            resumen.Lineas.RemoveAll(l => l.HabitacionID == habitacionId);
            SaveResumen(resumen);

            if (IsAjax)
            {
                var html = await this.RenderViewAsync("_ResumenReservaSidebar", resumen, partial: true);
                return Json(new { ok = true, resumenHtml = html });
            }

            return RedirectToAction(nameof(Confirmar));
        }

        [HttpPost("ActualizarPrecioLinea")]
        [ValidateAntiForgeryToken]
        public IActionResult ActualizarPrecioLinea(int habitacionId, decimal precioNoche)
        {
            var r = GetResumen();
            if (r == null)
                return BadRequest("Sesión de reserva perdida.");

            var linea = r.Lineas.FirstOrDefault(l => l.HabitacionID == habitacionId);
            if (linea == null)
                return BadRequest("Habitación no encontrada en el resumen.");

            // Si viene un valor raro, no lo cambio
            if (precioNoche <= 0)
                precioNoche = linea.PrecioNoche;

            // Aseguramos tener precio de lista (por si acaso)
            if (linea.PrecioNocheOriginal <= 0)
                linea.PrecioNocheOriginal = linea.PrecioNoche;

            // Actualizar precio acordado
            linea.PrecioNoche = precioNoche;

            // Recalcular subtotales de ESA línea con la misma lógica de antes
            var (baseNeta, inguat, iva, total) =
                ReservaPricingService.DesglosarDesdeTotal(precioNoche, linea.Noches);

            linea.Subtotal = baseNeta;
            linea.Impuestos = inguat + iva;
            linea.Total = total;

            // Guardar en sesión para que ConfirmarPost use estos valores
            SaveResumen(r);

            // En Confirmar queremos que el resumen sea "solo lectura"
            ViewData["ResumenEditable"] = false;

            // Devolvemos SOLO el partial del sidebar actualizado
            return PartialView("_ResumenReservaSidebar", r);
        }



    }

}