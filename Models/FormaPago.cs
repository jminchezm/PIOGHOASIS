namespace PIOGHOASIS.Models
{
    public class FormaPago
    {
        public short FormaPagoID { get; set; }
        public string Codigo { get; set; } = "";
        public string Nombre { get; set; } = "";
        public bool Estado { get; set; } = true;
    }
}
