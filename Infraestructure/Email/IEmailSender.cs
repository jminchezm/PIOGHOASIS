namespace PIOGHOASIS.Infraestructure.Email
{
    // Infra/Email/IEmailSender.cs
    public interface IEmailSender
    {
        Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
    }

}
