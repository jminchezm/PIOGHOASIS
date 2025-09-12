// Email/SmtpEmailSender.cs
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using PIOGHOASIS.Infraestructure.Email;
using System.Net;
using System.Net.Mail;

public sealed class SmtpEmailSender : Microsoft.AspNetCore.Identity.UI.Services.IEmailSender
{
    private readonly SmtpOptions _opt;
    public SmtpEmailSender(IOptions<SmtpOptions> opt) => _opt = opt.Value;

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        using var msg = new MailMessage
        {
            From = new MailAddress(_opt.FromEmail, _opt.FromName),
            Subject = subject,
            Body = htmlMessage,
            IsBodyHtml = true
        };
        msg.To.Add(email);

        using var smtp = new SmtpClient(_opt.Host, _opt.Port)
        {
            EnableSsl = _opt.UseSsl
        };
        if (!string.IsNullOrWhiteSpace(_opt.User))
            smtp.Credentials = new NetworkCredential(_opt.User, _opt.Password);

        await smtp.SendMailAsync(msg);
    }
}
