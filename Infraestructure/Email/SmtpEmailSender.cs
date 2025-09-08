// Infra/Email/SmtpEmailSender.cs
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using PIOGHOASIS.Infraestructure.Email;

public class SmtpEmailSender : IEmailSender
{
    private readonly EmailSettings _cfg;
    public SmtpEmailSender(IOptions<EmailSettings> cfg) => _cfg = cfg.Value;

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        using var msg = new MailMessage();
        msg.From = new MailAddress(_cfg.From.Contains('<') ? _cfg.From.Split('<', '>')[1] : _cfg.From,
                                   _cfg.From.Contains('<') ? _cfg.From.Split('<', '>')[0].Trim() : _cfg.From);
        msg.To.Add(to);
        msg.Subject = subject;
        msg.IsBodyHtml = true;
        msg.Body = htmlBody;

        using var smtp = new SmtpClient(_cfg.Host, _cfg.Port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(_cfg.User, _cfg.Pass)
        };
        await smtp.SendMailAsync(msg);
    }
}
