namespace PIOGHOASIS.Models.ViewModels
{
    public class ClienteFormVm
    {
        public Cliente Cliente { get; set; } = new Cliente();
        public Persona Persona { get; set; } = new Persona();
        public IFormFile? Foto { get; set; }   // archivo opcional
    }
}
