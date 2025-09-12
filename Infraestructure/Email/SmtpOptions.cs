namespace PIOGHOASIS.Infraestructure.Email
{
    public class SmtpOptions
    {

        public string Host { get; set; } = "";
        public int Port { get; set; } = 587;
        public string? User { get; set; }
        public string? Password { get; set; }
        public string FromName { get; set; } = "Hotel Oasis";
        public string FromEmail { get; set; } = "yoshuamm11@gmail.com";
        public bool UseSsl { get; set; } = true;

    }
}
