using Microsoft.EntityFrameworkCore;
using PIOGHOASIS.Infraestructure.Data;

namespace PIOGHOASIS.Services
{
    public interface IReservaPricingService
    {
        Task<bool> HabitacionDisponible(int habitacionId, DateTime inDate, DateTime outDate, int? excludeReservaId = null);
        Task<(decimal precio, int? tarifaId)> PrecioPorNoche(int habitacionId, int personas, DateTime inDate, DateTime outDate);
    }

    public class ReservaPricingService : IReservaPricingService
    {
        private readonly AppDbContext _db;
        private const decimal IVA = 0.12m; // ejemplo: 12% IVA

        public ReservaPricingService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<bool> HabitacionDisponible(int habitacionId, DateTime inDate, DateTime outDate, int? excludeReservaId = null)
        {
            var estadosQueBloquean = new[] { "RESERVADA", "CONFIRMADA" };
            var idsBloquean = await _db.estadosReserva
                              .Where(e => estadosQueBloquean.Contains(e.Codigo))
                              .Select(e => e.EstadoReservaID)
                              .ToListAsync();

            var q = _db.detalleReservas
                .Include(d => d.Reserva)
                .Where(d => d.HabitacionID == habitacionId
                    && idsBloquean.Contains(d.Reserva.EstadoReservaID)
                    && !(outDate <= d.Reserva.FechaCheckIn || inDate >= d.Reserva.FechaCheckOut));

            if (excludeReservaId.HasValue)
                q = q.Where(d => d.ReservaID != excludeReservaId.Value);

            return !await q.AnyAsync();
        }

        public async Task<(decimal precio, int? tarifaId)> PrecioPorNoche(int habitacionId, int personas, DateTime inDate, DateTime outDate)
        {
            var t = await _db.tarifasHabitacion
                .Where(x => x.HabitacionID == habitacionId
                         && x.NumeroPersonas == personas
                         && x.FechaInicio <= inDate
                         && x.FechaFin >= outDate.AddDays(-1))
                .OrderBy(x => x.PrecioNoche)
                .FirstOrDefaultAsync();

            if (t == null) return (0m, null);
            return (t.PrecioNoche, t.TarifaID);
        }

        public static (decimal sub, decimal imp, decimal tot) Totales(decimal precioNoche, int noches)
        {
            var sub = precioNoche * noches;
            var imp = Math.Round(sub * IVA, 2);
            var tot = sub + imp;
            return (sub, imp, tot);
        }
    }
}
