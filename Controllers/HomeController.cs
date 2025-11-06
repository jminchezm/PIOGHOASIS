using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PIOGHOASIS.Infraestructure.Data;
using PIOGHOASIS.Models;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace PIOGHOASIS.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _db;

        public HomeController(ILogger<HomeController> logger, AppDbContext db)
        {
            _logger = logger;
            _db = db;
        }

        // Opcional: protege el dashboard
        [Authorize]
        [HttpGet]
        public IActionResult Dashboard()
        {
            return View();  // busca Views/Home/Dashboard.cshtml
        }

        // Si alguien entra a /Home/Index, lo mandamos al Dashboard
        [HttpGet]
        public IActionResult Index() => RedirectToAction(nameof(Dashboard));

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        // ===== Datos para las tarjetas del Dashboard =====
        [HttpGet("Home/GetDashboardData")]
        public async Task<IActionResult> GetDashboardData()
        {
            var hoy = DateTime.Today;

            // Reservas con check-in hoy y aún pendientes o confirmadas
            var reservasPendientes = await _db.reservas
                .Include(r => r.Estado)
                .Where(r => r.FechaCheckIn.Date == hoy &&
                            (
                                (r.Estado.Nombre ?? "").Contains("Reservada") 
                                //||
                                //(r.Estado.Nombre ?? "").Contains("Confirmada")
                            ))
                .CountAsync();

            // Habitaciones activas y no ocupadas hoy
            var habitacionesTotales = await _db.habitaciones.CountAsync(h => h.Estado);

            var habitacionesOcupadas = await _db.reservas
                .Where(r => r.FechaCheckIn.Date <= hoy && r.FechaCheckOut.Date > hoy)
                .SelectMany(r => r.Detalles)
                .Select(d => d.HabitacionID)
                .Distinct()
                .CountAsync();

            var habitacionesDisponibles = habitacionesTotales - habitacionesOcupadas;

            // Ingresos del día
            var ingresosDia = await _db.pagosReserva
                .Where(p => p.FechaPago.Date == hoy)
                .SumAsync(p => (decimal?)p.MontoPagado) ?? 0m;

            // Clientes alojados actualmente
            var clientesActuales = await _db.reservas
                .Where(r => r.FechaCheckIn.Date <= hoy && r.FechaCheckOut.Date > hoy)
                .Select(r => r.ClienteID)
                .Distinct()
                .CountAsync();

            return Json(new
            {
                reservasPendientes,
                habitacionesDisponibles,
                ingresosDia,
                clientesActuales
            });
        }
    }
}
