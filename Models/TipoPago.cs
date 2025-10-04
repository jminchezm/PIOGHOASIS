namespace PIOGHOASIS.Models
{
    public class TipoPago
    {

        public short TipoPagoID { get; set; }
        public string Codigo { get; set; } = "";
        public string Nombre { get; set; } = "";
        public bool Estado { get; set; } = true;

    }
}
