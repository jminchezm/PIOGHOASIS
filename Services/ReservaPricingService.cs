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

        // === NUEVAS CONSTANTES DE IMPUESTOS ===
        // 10% INGUAT y 12% IVA
        private const decimal INGUAT_RATE = 0.10m;
        private const decimal IVA_RATE = 0.12m;

        public ReservaPricingService(AppDbContext db)
        {
            _db = db;
        }

        // --------------------------------------------------------------------
        // Verifica si una habitación está disponible en un rango de fechas
        // --------------------------------------------------------------------
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

        // --------------------------------------------------------------------
        // Obtiene el precio POR NOCHE (se asume YA CON IMPUESTOS) y la tarifa
        // --------------------------------------------------------------------
        public async Task<(decimal precio, int? tarifaId)> PrecioPorNoche(int habitacionId, int personas, DateTime inDate, DateTime outDate)
        {
            var t = await _db.tarifasHabitacion
                .Where(x => x.HabitacionID == habitacionId
                            && x.NumeroPersonas == personas
                            && x.FechaInicio <= inDate
                            && x.FechaFin >= outDate.AddDays(-1))
                .OrderBy(x => x.PrecioNoche)
                .FirstOrDefaultAsync();

            if (t == null)
                return (0m, null);

            // t.PrecioNoche se considera PRECIO DE VENTA por noche (incluye INGUAT + IVA)
            return (t.PrecioNoche, t.TarifaID);
        }

        // --------------------------------------------------------------------
        // NUEVO: Desglosa un precio TOTAL (con INGUAT+IVA) en base + INGUAT + IVA
        // precioTotalNoche = precio de venta por noche (con impuestos incluidos)
        // --------------------------------------------------------------------

        public static (decimal baseNeta, decimal inguat, decimal iva, decimal total)
DesglosarDesdeTotal(decimal precioTotalNoche, int noches = 1)
        {
            // Total por noche canon (2 decimales)
            var totalPorNoche = decimal.Round(precioTotalNoche, 2, MidpointRounding.AwayFromZero);

            var factor = 1 + INGUAT_RATE + IVA_RATE; // 1.22

            // Calculamos base sin redondear, luego redondeamos
            var baseNetaRaw = totalPorNoche / factor;
            var baseNetaPorNoche = decimal.Round(baseNetaRaw, 2, MidpointRounding.AwayFromZero);

            var inguatPorNoche = decimal.Round(
                baseNetaPorNoche * INGUAT_RATE,
                2, MidpointRounding.AwayFromZero);

            var ivaPorNoche = decimal.Round(
                baseNetaPorNoche * IVA_RATE,
                2, MidpointRounding.AwayFromZero);

            // Ajuste de 1 centavo (o -1) para que la suma coincida con el total
            var sumaComp = baseNetaPorNoche + inguatPorNoche + ivaPorNoche;
            var diff = totalPorNoche - sumaComp;   // estará entre -0.02 y 0.02 normalmente

            if (diff != 0)
            {
                // Le cargamos el centavo sobrante/faltante al IVA (podría ser a la base o INGUAT, da igual)
                ivaPorNoche += diff;
            }

            if (noches <= 1)
                return (baseNetaPorNoche, inguatPorNoche, ivaPorNoche, totalPorNoche);

            // Para N noches: simplemente multiplicamos cada componente
            var baseTotal = baseNetaPorNoche * noches;
            var inguatTotal = inguatPorNoche * noches;
            var ivaTotal = ivaPorNoche * noches;
            var total = totalPorNoche * noches; // siempre precioTotalNoche * noches

            return (baseTotal, inguatTotal, ivaTotal, total);
        }


        //public static (decimal baseNeta, decimal inguat, decimal iva, decimal total)
        //DesglosarDesdeTotal(decimal precioTotalNoche, int noches = 1)
        //{
        //    var factor = 1 + INGUAT_RATE + IVA_RATE; // 1.22

        //    var baseNetaPorNoche = decimal.Round(
        //        precioTotalNoche / factor,
        //        2, MidpointRounding.AwayFromZero);

        //    var inguatPorNoche = decimal.Round(
        //        baseNetaPorNoche * INGUAT_RATE,
        //        2, MidpointRounding.AwayFromZero);

        //    var ivaPorNoche = decimal.Round(
        //        baseNetaPorNoche * IVA_RATE,
        //        2, MidpointRounding.AwayFromZero);

        //    var totalPorNoche = baseNetaPorNoche + inguatPorNoche + ivaPorNoche;

        //    if (noches <= 1)
        //        return (baseNetaPorNoche, inguatPorNoche, ivaPorNoche, totalPorNoche);

        //    return (baseNetaPorNoche * noches,
        //            inguatPorNoche * noches,
        //            ivaPorNoche * noches,
        //            totalPorNoche * noches);
        //}


        // --------------------------------------------------------------------
        // Helper genérico: dado un PRECIO TOTAL por noche (con impuestos),
        // devuelve:
        //   sub = base sin impuestos (todas las noches)
        //   imp = INGUAT + IVA (todas las noches)
        //   tot = total con impuestos (todas las noches)
        // --------------------------------------------------------------------
        public static (decimal sub, decimal imp, decimal tot) Totales(decimal precioTotalNoche, int noches)
        {
            var (baseNeta, inguat, iva, total) = DesglosarDesdeTotal(precioTotalNoche, noches);
            var imp = inguat + iva;
            return (baseNeta, imp, total);
        }
    }
}
