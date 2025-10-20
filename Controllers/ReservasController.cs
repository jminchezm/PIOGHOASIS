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

        // Listado (mock)
        //[HttpGet("")]
        //[HttpGet("Index")]
        //public async Task<IActionResult> Index()
        //{
        //    var list = await _db.reservas
        //        .Include(r => r.Cliente).ThenInclude(c => c.Persona)
        //        .Include(r => r.Detalles).ThenInclude(d => d.Habitacion)
        //        .OrderByDescending(r => r.ReservaID)
        //        .Take(50).AsNoTracking().ToListAsync();

        //    return View(list);
        //}

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
        [HttpGet("ElegirHabitacion")]
        public IActionResult ElegirHabitacion() => View(new BusquedaHabitacionVM());

        [HttpGet("Buscar")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Buscar(DateTime? checkIn, DateTime? checkOut, int personas = 1)
        {
            // === Caso sin fechas o fechas inválidas: devuelve parcial vacío (muestra la alerta)
            if (checkIn == null || checkOut == null || checkOut <= checkIn)
            {
                var vacio = new List<HabitacionDisponibleVM>();

                if (IsAjax) // <-- usa tu helper ya existente en el controlador
                    return PartialView("_ResultadosHabitaciones", vacio);

                // Navegación full: reconstruye la vista host y embebe el parcial dentro del panel blanco
                ViewData["Resultados"] = await this.RenderViewAsync("_ResultadosHabitaciones", vacio, partial: true);
                return View("ElegirHabitacion", new BusquedaHabitacionVM());
            }

            var ci = checkIn.Value.Date;
            var co = checkOut.Value.Date;

            // 1) Estados que BLOQUEAN inventario
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

            var res = new List<HabitacionDisponibleVM>();

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
                            var (_, _, tot) = ReservaPricingService.Totales(t.PrecioNoche, 1);
                            item.Tarifas.Add(new TarifaOpcionVM
                            {
                                Personas = p,
                                TarifaID = t.TarifaID,
                                PrecioNoche = t.PrecioNoche,
                                TotalConImpuestos = tot,
                                Etiqueta = string.IsNullOrWhiteSpace(t.EtiquetaTemporada)
                                           ? $"Tarifa {p} persona(s)"
                                           : t.EtiquetaTemporada
                            });
                        }
                    }
                    else
                    {
                        var (precio, tarifaId) = await _pricing.PrecioPorNoche(h.HabitacionID, p, ci, co);
                        if (precio > 0)
                        {
                            var (_, _, tot) = ReservaPricingService.Totales(precio, 1);
                            item.Tarifas.Add(new TarifaOpcionVM
                            {
                                Personas = p,
                                TarifaID = tarifaId,
                                PrecioNoche = precio,
                                TotalConImpuestos = tot,
                                Etiqueta = "Tarifa estándar"
                            });
                        }
                    }
                }

                var active = item.Tarifas.FirstOrDefault(t => t.Personas == defaultPersonas)
                          ?? item.Tarifas.FirstOrDefault();

                if (active != null)
                {
                    item.TarifaSeleccionadaID = active.TarifaID;
                    item.PrecioNoche = active.PrecioNoche;
                    item.TotalConImpuestos = active.TotalConImpuestos;
                }

                res.Add(item);
            }

            // === Respuesta según contexto (AJAX vs navegación completa)
            if (IsAjax)
                return PartialView("_ResultadosHabitaciones", res);

            ViewData["Resultados"] = await this.RenderViewAsync("_ResultadosHabitaciones", res, partial: true);
            return View("ElegirHabitacion", new BusquedaHabitacionVM
            {
                CheckIn = checkIn,
                CheckOut = checkOut,
                Personas = personas
            });
        }


        //[HttpGet("Buscar")]
        //[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        //public async Task<IActionResult> Buscar(DateTime? checkIn, DateTime? checkOut, int personas = 1)
        //{
        //    if (checkIn == null || checkOut == null || checkOut <= checkIn)
        //        return PartialView("_ResultadosHabitaciones", new List<HabitacionDisponibleVM>());

        //    var ci = checkIn.Value.Date;
        //    var co = checkOut.Value.Date;

        //    // 1) Estados que BLOQUEAN inventario (ajusta los códigos a los tuyos)
        //    var codigosBloqueo = new[] { "ESTHAB0001", "ESTHAB0002"}; // Reservada, Confirmada, Check-in (ejemplo)
        //    var idsBloqueo = await _db.estadosReserva
        //        .Where(e => codigosBloqueo.Contains(e.Codigo))
        //        .Select(e => e.EstadoReservaID)
        //        .ToListAsync();

        //    // 2) Si no usas códigos, puedes resolver por nombre (fallback):
        //    if (!idsBloqueo.Any())
        //    {
        //        var nombres = new[] { "RESERVADA", "CONFIRMADA"}; // usa begins-with "CHECK" por si es "Check-in"
        //        idsBloqueo = await _db.estadosReserva
        //            .Where(e => nombres.Any(n => e.Nombre.ToUpper().Contains(n)))
        //            .Select(e => e.EstadoReservaID)
        //            .ToListAsync();
        //    }

        //    // 3) Buscar habitaciones DISPONIBLES: no tengan solape con reservas en estados bloqueantes
        //    var habsDisponibles = await _db.habitaciones
        //        .Where(h => h.Estado)
        //        .Include(h => h.TipoHabitacion)
        //        .Where(h => !_db.detalleReservas.Any(d =>
        //            d.HabitacionID == h.HabitacionID &&
        //            idsBloqueo.Contains(d.Reserva.EstadoReservaID) &&      // ← ya no hardcodeado
        //            !(d.Reserva.FechaCheckOut <= ci || d.Reserva.FechaCheckIn >= co)))
        //        .AsNoTracking()
        //        .ToListAsync();

        //    var res = new List<HabitacionDisponibleVM>();

        //    foreach (var h in habsDisponibles)
        //    {
        //        var cap = Math.Max(1, (int)(h.CapacidadPersonas ?? 1));
        //        var defaultPersonas = Math.Clamp(personas, 1, cap);

        //        var item = new HabitacionDisponibleVM
        //        {
        //            HabitacionID = h.HabitacionID,
        //            Codigo = h.Codigo,
        //            Imagen = string.IsNullOrWhiteSpace(h.Imagen) ? "/img/DefaultHabitacion.png" : h.Imagen,
        //            Titulo = $"{h.TipoHabitacion?.Nombre} #{h.NumeroHabitacion}",
        //            TipoNombre = h.TipoHabitacion?.Nombre ?? "",
        //            NumeroHabitacion = h.NumeroHabitacion,
        //            Capacidad = cap,
        //            PersonasSeleccionadas = defaultPersonas
        //        };

        //        // Catálogo de tarifas
        //        for (int p = 1; p <= cap; p++)
        //        {
        //            var tarifasDb = await _db.tarifasHabitacion
        //                .AsNoTracking()
        //                .Where(t => t.HabitacionID == h.HabitacionID
        //                         && t.NumeroPersonas == p
        //                         && t.FechaInicio <= ci
        //                         && t.FechaFin >= co.AddDays(-1))
        //                .OrderBy(t => t.PrecioNoche)
        //                .ToListAsync();

        //            if (tarifasDb.Count == 0)
        //            {
        //                tarifasDb = await _db.tarifasHabitacion
        //                    .AsNoTracking()
        //                    .Where(t => t.HabitacionID == h.HabitacionID
        //                             && t.NumeroPersonas == p
        //                             && !(t.FechaFin < ci || t.FechaInicio > co.AddDays(-1)))
        //                    .OrderBy(t => t.PrecioNoche)
        //                    .ToListAsync();
        //            }

        //            if (tarifasDb.Count > 0)
        //            {
        //                foreach (var t in tarifasDb)
        //                {
        //                    var (_, _, tot) = ReservaPricingService.Totales(t.PrecioNoche, 1);
        //                    item.Tarifas.Add(new TarifaOpcionVM
        //                    {
        //                        Personas = p,
        //                        TarifaID = t.TarifaID,
        //                        PrecioNoche = t.PrecioNoche,
        //                        TotalConImpuestos = tot,
        //                        Etiqueta = string.IsNullOrWhiteSpace(t.EtiquetaTemporada) ? $"Tarifa {p} persona(s)" : t.EtiquetaTemporada
        //                    });
        //                }
        //            }
        //            else
        //            {
        //                var (precio, tarifaId) = await _pricing.PrecioPorNoche(h.HabitacionID, p, ci, co);
        //                if (precio > 0)
        //                {
        //                    var (_, _, tot) = ReservaPricingService.Totales(precio, 1);
        //                    item.Tarifas.Add(new TarifaOpcionVM
        //                    {
        //                        Personas = p,
        //                        TarifaID = tarifaId,
        //                        PrecioNoche = precio,
        //                        TotalConImpuestos = tot,
        //                        Etiqueta = "Tarifa estándar"
        //                    });
        //                }
        //            }
        //        }

        //        var active = item.Tarifas.FirstOrDefault(t => t.Personas == defaultPersonas) ?? item.Tarifas.FirstOrDefault();
        //        if (active != null)
        //        {
        //            item.TarifaSeleccionadaID = active.TarifaID;
        //            item.PrecioNoche = active.PrecioNoche;
        //            item.TotalConImpuestos = active.TotalConImpuestos;
        //        }

        //        res.Add(item);
        //    }

        //    return PartialView("_ResultadosHabitaciones", res);
        //}


        //[HttpGet("Buscar")]
        //[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        //public async Task<IActionResult> Buscar(DateTime? checkIn, DateTime? checkOut, int personas = 1)
        //{
        //    if (checkIn == null || checkOut == null || checkOut <= checkIn)
        //        return PartialView("_ResultadosHabitaciones", new List<HabitacionDisponibleVM>());

        //    var ci = checkIn.Value.Date;
        //    var co = checkOut.Value.Date;

        //    var habsDisponibles = await _db.habitaciones
        //        .Where(h => h.Estado)                         // bool directo, OK
        //        .Include(h => h.TipoHabitacion)               // esto sí es válido aquí
        //        .Where(h => !_db.detalleReservas.Any(d =>     // <-- sin Include en subquery
        //            d.HabitacionID == h.HabitacionID &&
        //            (d.Reserva.EstadoReservaID == 1 ||        // estados que bloquean inventario
        //             d.Reserva.EstadoReservaID == 2 ||
        //             d.Reserva.EstadoReservaID == 3) &&
        //            !(d.Reserva.FechaCheckOut <= ci || d.Reserva.FechaCheckIn >= co)
        //        ))
        //        .AsNoTracking()
        //        .ToListAsync();

        //    var res = new List<HabitacionDisponibleVM>();

        //    foreach (var h in habsDisponibles)
        //    {
        //        var cap = Math.Max(1, (int)(h.CapacidadPersonas ?? 1));
        //        var defaultPersonas = Math.Clamp(personas, 1, cap);

        //        var item = new HabitacionDisponibleVM
        //        {
        //            HabitacionID = h.HabitacionID,
        //            Codigo = h.Codigo,
        //            Imagen = string.IsNullOrWhiteSpace(h.Imagen) ? "/img/DefaultHabitacion.png" : h.Imagen,
        //            Titulo = $"{h.TipoHabitacion?.Nombre} #{h.NumeroHabitacion}",
        //            TipoNombre = h.TipoHabitacion?.Nombre ?? "",
        //            NumeroHabitacion = h.NumeroHabitacion,
        //            Capacidad = cap,
        //            PersonasSeleccionadas = defaultPersonas
        //        };

        //        // Catálogo de tarifas por #personas
        //        for (int p = 1; p <= cap; p++)
        //        {

        //            var tarifasDb = await _db.tarifasHabitacion
        //            .AsNoTracking()
        //            .Where(t => t.HabitacionID == h.HabitacionID
        //                     && t.NumeroPersonas == p
        //                     && t.FechaInicio <= ci
        //                     && t.FechaFin >= co.AddDays(-1))
        //            .OrderBy(t => t.PrecioNoche)
        //            .ToListAsync();

        //            // 2) Si no encontró nada, prueba con "se solapa con el rango"
        //            if (tarifasDb.Count == 0)
        //            {

        //                tarifasDb = await _db.tarifasHabitacion
        //                .AsNoTracking()
        //                .Where(t => t.HabitacionID == h.HabitacionID
        //                         && t.NumeroPersonas == p
        //                         && !(t.FechaFin < ci || t.FechaInicio > co.AddDays(-1)))
        //                .OrderBy(t => t.PrecioNoche)
        //                .ToListAsync();

        //            }

        //            if (tarifasDb.Count > 0)
        //            {
        //                foreach (var t in tarifasDb)
        //                {
        //                    var (_, imp, tot) = ReservaPricingService.Totales(t.PrecioNoche, 1);
        //                    item.Tarifas.Add(new TarifaOpcionVM
        //                    {
        //                        Personas = p,
        //                        TarifaID = t.TarifaID,
        //                        PrecioNoche = t.PrecioNoche,
        //                        TotalConImpuestos = tot,
        //                        Etiqueta = string.IsNullOrWhiteSpace(t.EtiquetaTemporada)
        //                                                ? $"Tarifa {p} persona(s)"
        //                                                : t.EtiquetaTemporada
        //                    });
        //                }
        //            }
        //            else
        //            {
        //                // Fallback: pricing service
        //                var (precio, tarifaId) = await _pricing.PrecioPorNoche(h.HabitacionID, p, ci, co);
        //                if (precio > 0)
        //                {
        //                    var (_, imp, tot) = ReservaPricingService.Totales(precio, 1);
        //                    item.Tarifas.Add(new TarifaOpcionVM
        //                    {
        //                        Personas = p,
        //                        TarifaID = tarifaId,
        //                        PrecioNoche = precio,
        //                        TotalConImpuestos = tot,
        //                        Etiqueta = "Tarifa estándar"
        //                    });
        //                }
        //            }
        //        }

        //        // Selección activa (la que coincide con "personas" del filtro)
        //        var active = item.Tarifas.FirstOrDefault(t => t.Personas == defaultPersonas)
        //                  ?? item.Tarifas.FirstOrDefault();
        //        if (active != null)
        //        {
        //            item.TarifaSeleccionadaID = active.TarifaID;
        //            item.PrecioNoche = active.PrecioNoche;
        //            item.TotalConImpuestos = active.TotalConImpuestos;
        //        }

        //        res.Add(item);
        //    }

        //    return PartialView("_ResultadosHabitaciones", res);
        //}

        // Selecciona 1 habitación y pasa a resumen
        [HttpPost("Seleccionar")]
        public async Task<IActionResult> Seleccionar(int habitacionId, DateTime checkIn, DateTime checkOut, int personas, int? tarifaId)
        {
            var h = await _db.habitaciones.Include(x => x.TipoHabitacion)
                                          .FirstOrDefaultAsync(x => x.HabitacionID == habitacionId);
            if (h == null) return NotFound();

            var cap = Math.Max(1, (int)(h.CapacidadPersonas ?? 1));
            personas = Math.Clamp(personas, 1, cap);

            decimal precio;
            int? tarifaUsada;

            if (tarifaId.HasValue)
            {
                // Validar la tarifa elegida
                var t = await _db.tarifasHabitacion.FirstOrDefaultAsync(x =>
                    x.TarifaID == tarifaId.Value &&
                    x.HabitacionID == habitacionId &&
                    x.NumeroPersonas == personas &&
                    // rango de fechas: [FechaInicio, FechaFin] cubre TODO el rango (ajusta a tu regla)
                    x.FechaInicio <= checkIn.Date && x.FechaFin >= checkOut.Date.AddDays(-1));

                if (t == null)
                {
                    // si la tarifa enviada no es válida, volvemos al pricing service
                    var r = await _pricing.PrecioPorNoche(habitacionId, personas, checkIn, checkOut);
                    precio = r.precio;
                    tarifaUsada = r.tarifaId;
                }
                else
                {
                    precio = t.PrecioNoche;
                    tarifaUsada = t.TarifaID;
                }
            }
            else
            {
                var r = await _pricing.PrecioPorNoche(habitacionId, personas, checkIn, checkOut);
                precio = r.precio;
                tarifaUsada = r.tarifaId;
            }

            var noches = (int)(checkOut.Date - checkIn.Date).TotalDays;
            var (sub, imp, tot) = ReservaPricingService.Totales(precio, noches);

            //var resumen = new List<ReservaResumenVM>();

            var resumen = new ReservaResumenVM
            {
                CheckIn = checkIn.Date,
                CheckOut = checkOut.Date,
                Noches = noches,
                Personas = personas,
                HabitacionID = habitacionId,
                HabitacionTitulo = $"{h.TipoHabitacion?.Nombre} #{h.NumeroHabitacion}",
                PrecioNoche = precio,
                TarifaID = tarifaUsada,
                Subtotal = sub,
                Impuestos = imp,
                Total = tot
            };

            HttpContext.Session.SetString(KEY, System.Text.Json.JsonSerializer.Serialize(resumen));

            // Si viene por AJAX devolvemos una URL para redirigir al paso Cliente
            if (Request.Headers.TryGetValue("X-Requested-With", out var xh) &&
                string.Equals(xh.ToString(), "XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { ok = true, redirectUrl = Url.Action(nameof(Cliente)) });
            }

            // Navegación normal
            return RedirectToAction(nameof(Cliente));
        }

        // Paso 2: Cliente
        [HttpGet("Cliente")]
        public async Task<IActionResult> Cliente()
        {
            var resumen = GetResumen();
            if (resumen == null) return RedirectToAction(nameof(ElegirHabitacion));

            var vm = new ReservaCreateVM
            {
                Resumen = resumen,
                Clientes = await _db.clientes.Include(c => c.Persona).Where(c => c.Estado).AsNoTracking().ToListAsync()
            };
            return View(vm);
        }

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
            return View(resumen);
        }

        [HttpPost("Confirmar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarPost()
        {
            var r = GetResumen();
            if (r == null || r.ClienteID == null)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { ok = false, msg = "Sesión de reserva perdida.", redirectUrl = Url.Action(nameof(ElegirHabitacion)) });
                return RedirectToAction(nameof(ElegirHabitacion));
            }

            // Verificación final de disponibilidad
            if (!await _pricing.HabitacionDisponible(r.HabitacionID, r.CheckIn, r.CheckOut))
            {
                var msg = "La habitación ya no está disponible.";
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { ok = false, msg, redirectUrl = Url.Action(nameof(ElegirHabitacion)) });

                TempData["msg"] = msg;
                return RedirectToAction(nameof(ElegirHabitacion));
            }

            using var tx = await _db.Database.BeginTransactionAsync();
            try
            {
                var reserva = new Reserva
                {
                    ClienteID = r.ClienteID!,
                    UsuarioID = GetUserId(),
                    UsuarioFinaliza = null,
                    EstadoReservaID = await EstadoId("ESTHAB0001"), // "Reservada" (ajusta si corresponde)
                    FechaCheckIn = r.CheckIn,
                    FechaCheckOut = r.CheckOut,
                    Subtotal = r.Subtotal,
                    Impuestos = r.Impuestos,
                    Total = r.Total,
                    Codigo = await NextCodigoAsync()
                };

                _db.reservas.Add(reserva);
                await _db.SaveChangesAsync();

                var det = new DetalleReserva
                {
                    ReservaID = reserva.ReservaID,
                    HabitacionID = r.HabitacionID,
                    Personas = r.Personas,
                    Noches = r.Noches,
                    PrecioPorNoche = r.PrecioNoche,
                    TotalLinea = r.PrecioNoche * r.Noches,
                    TarifaID = r.TarifaID
                };
                _db.detalleReservas.Add(det);
                await _db.SaveChangesAsync();

                await tx.CommitAsync();
                HttpContext.Session.Remove(KEY);

                var url = Url.Action(nameof(Detalles), new { id = reserva.ReservaID });

                // 🔁 Si viene por AJAX devolvemos JSON (lo usa el script para modal + redirección)
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { ok = true, redirectUrl = url });

                // Navegación tradicional
                return RedirectToAction(nameof(Detalles), new { id = reserva.ReservaID });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return StatusCode(500, new { ok = false, msg = "Error al confirmar.", detail = ex.Message });

                throw;
            }
        }


        //[HttpPost("Confirmar")]
        //public async Task<IActionResult> ConfirmarPost()
        //{
        //    var r = GetResumen();
        //    if (r == null || r.ClienteID == null) return RedirectToAction(nameof(ElegirHabitacion));

        //    // Verifica disponibilidad última vez
        //    if (!await _pricing.HabitacionDisponible(r.HabitacionID, r.CheckIn, r.CheckOut))
        //    {
        //        TempData["msg"] = "La habitación ya no está disponible.";
        //        return RedirectToAction(nameof(ElegirHabitacion));
        //    }

        //    using var tx = await _db.Database.BeginTransactionAsync();
        //    try
        //    {
        //        var reserva = new Reserva
        //        {
        //            ClienteID = r.ClienteID!,
        //            UsuarioID = GetUserId(),
        //            //usuarioIDNombre = ClaimTypes.NameIdentifier,
        //            UsuarioFinaliza = null,
        //            //usuarioFinalizaNombre = null,
        //            //usuarioFinalizaNombre = reserva.USUARIO
        //            EstadoReservaID = await EstadoId("ESTHAB0001"),
        //            FechaCheckIn = r.CheckIn,
        //            FechaCheckOut = r.CheckOut,
        //            Subtotal = r.Subtotal,
        //            Impuestos = r.Impuestos,
        //            Total = r.Total,
        //            Codigo = await NextCodigoAsync()
        //        };

        //        _db.reservas.Add(reserva);
        //        await _db.SaveChangesAsync();

        //        var det = new DetalleReserva
        //        {
        //            ReservaID = reserva.ReservaID,
        //            HabitacionID = r.HabitacionID,
        //            Personas = r.Personas,
        //            Noches = r.Noches,
        //            PrecioPorNoche = r.PrecioNoche,
        //            TotalLinea = r.PrecioNoche * r.Noches,
        //            TarifaID = r.TarifaID
        //        };
        //        _db.detalleReservas.Add(det);
        //        await _db.SaveChangesAsync();

        //        await tx.CommitAsync();
        //        HttpContext.Session.Remove(KEY);

        //        return RedirectToAction(nameof(Detalles), new { id = reserva.ReservaID });
        //    }
        //    catch
        //    {
        //        await tx.RollbackAsync();
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

            // Si quieres pasar algo adicional por ViewData (opcional)
            // var vdd = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = r };
            // vdd["Algo"] = "Valor";

            var pdf = new ViewAsPdf("ReservaPdf", r)   // <-- busca Views/Reservas/ReservaPdf.cshtml
            {
                PageSize = Size.A4,
                PageOrientation = Orientation.Portrait,
                PageMargins = new Margins { Top = 18, Right = 14, Bottom = 16, Left = 14 },
                CustomSwitches = "--print-media-type --footer-center \"Página [page] de [toPage]\" --footer-font-size 8 --footer-spacing 5",
                ContentDisposition = descargar ? ContentDisposition.Attachment : ContentDisposition.Inline,
                // ViewData = vdd
            };

            if (descargar)
                pdf.FileName = $"Reserva_{r.Codigo}.pdf";

            return pdf;
        }



        // PagosController.cs
        //[Route("Pagos")]
        //    public class PagosController : Controller
        //    {
        //        private readonly AppDbContext _db;
        //        public PagosController(AppDbContext db) => _db = db;

        //        // Pantalla para gestionar anticipos / cobro total
        //        [HttpGet("Nueva")]
        //        public async Task<IActionResult> Nueva(int reservaId)
        //        {
        //            var r = await _db.reservas
        //                .Include(x => x.Cliente).ThenInclude(c => c.Persona)
        //                .FirstOrDefaultAsync(x => x.ReservaID == reservaId);
        //            if (r == null) return NotFound();

        //            // TODO: devolver vista para elegir método de pago, registrar anticipo, etc.
        //            return View(r);
        //        }
        //    }

        //[HttpPost("Cancelar/{id:int}")]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Cancelar(int id, string? motivo)
        //{
        //    var reserva = await _db.reservas
        //        .Include(r => r.Estado)
        //        .FirstOrDefaultAsync(r => r.ReservaID == id);

        //    if (reserva == null) return NotFound();

        //    // Si ya está cancelada, no hacemos nada
        //    // (opcional) puedes permitir re-cancelar con otro motivo si quieres.
        //    if (string.Equals(reserva.Estado?.Codigo, reserva.Estado?.Codigo, StringComparison.OrdinalIgnoreCase))
        //        return RedirectToAction(nameof(Detalles), new { id });

        //    // obtener el ID de estado cancelado
        //    var idCancelada = await _db.estadosReserva
        //        .Where(e => e.Codigo == reserva.Estado.Codigo)
        //        .Select(e => e.EstadoReservaID)
        //        .FirstOrDefaultAsync();

        //    if (idCancelada == 0)
        //        throw new InvalidOperationException($"No existe el estado con código {reserva.Estado?.Codigo}. Crea ese registro en ESTADO_RESERVA.");

        //    // Cambiar estado
        //    reserva.EstadoReservaID = idCancelada;

        //    // (opcional) guarda el motivo en Observaciones de la reserva o en una bitácora
        //    if (!string.IsNullOrWhiteSpace(motivo))
        //    {
        //        reserva.NotaCancelacion = string.IsNullOrWhiteSpace(reserva.NotaCancelacion)
        //            ? $"[CANCELADA] {DateTime.Now:yyyy-MM-dd HH:mm}: {motivo}"
        //            : $"{reserva.NotaCancelacion}{Environment.NewLine}[CANCELADA] {DateTime.Now:yyyy-MM-dd HH:mm}: {motivo}";
        //    }

        //    await _db.SaveChangesAsync();

        //    // IMPORTANTE: tu consulta de disponibilidad de habitación **bloquea** solo estados 1/2/3.
        //    // Al poner estado “Cancelada” con otro código (p.ej. ESTHAB0004),
        //    // la habitación volverá a figurar disponible en búsquedas automáticamente.

        //    // Redirige a detalles (o al listado)
        //    return RedirectToAction(nameof(Detalles), new { id });
        //}

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


        //[HttpPost("Cancelar/{id:int}")]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Cancelar(
        //int id,
        //string? motivo,
        //string? estadoCodigo,   // <-- opcional
        //string? estadoNombre)   // <-- opcional
        //{
        //    var reserva = await _db.reservas
        //        .Include(r => r.Estado)
        //        .FirstOrDefaultAsync(r => r.ReservaID == id);
        //    if (reserva == null) return NotFound();

        //    var idCancelada = await ResolverEstadoId(
        //        codigo: estadoCodigo,
        //        nombre: estadoNombre,
        //        preferirCancelada: true);

        //    if (idCancelada == 0)
        //    {
        //        TempData["msg"] = "No se encontró un estado 'Cancelada'. Configúrelo en ESTADO_RESERVA.";
        //        return RedirectToAction(nameof(Index), new { id });
        //    }

        //    if (reserva.EstadoReservaID == idCancelada)
        //        return RedirectToAction(nameof(Index), new { id });

        //    reserva.EstadoReservaID = idCancelada;

        //    if (!string.IsNullOrWhiteSpace(motivo))
        //    {
        //        reserva.NotaCancelacion = string.IsNullOrWhiteSpace(reserva.NotaCancelacion)
        //            ? $"[CANCELADA] {DateTime.Now:yyyy-MM-dd HH:mm}: {motivo}"
        //            : $"{reserva.NotaCancelacion}{Environment.NewLine}[CANCELADA] {DateTime.Now:yyyy-MM-dd HH:mm}: {motivo}";
        //    }

        //    await _db.SaveChangesAsync();
        //    //return RedirectToAction(nameof(Index), new { id });
        //    return RedirectToAction(nameof(Index), new { id });

        //    //return RedirectToAction("Index", "Reservas", new { reservaId = id });

        //    //TempData["FlashModal"] = "Reserva cancelada correctamente.";
        //    //TempData["FlashType"] = "warning"; // por ejemplo
        //    //return RedirectToAction(nameof(Index));
        //}

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


    }

}