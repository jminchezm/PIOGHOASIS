namespace PIOGHOASIS.Models.ViewModels
{
    // Tipos compartidos por varios reportes
    public record ItemMonto(string Clave, decimal Monto);
    public record ItemMontoDate(DateTime Dia, decimal Monto);
}
