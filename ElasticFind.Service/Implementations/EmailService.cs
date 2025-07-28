using System.Net;
using System.Net.Mail;
using ElasticFind.Repository.ViewModels;
using ElasticFind.Service.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace ElasticFind.Service.Implementations;

public class EmailService : IEmailService
{
    private readonly SmtpSettings _smtpSettings;

    public EmailService(IOptions<SmtpSettings> smtpSettings)
    {
        if (smtpSettings?.Value == null)
        {
            throw new ArgumentNullException(nameof(smtpSettings), "SmtpSettings configuration is missing or invalid.");
        }
        _smtpSettings = smtpSettings.Value;
    }
    public async Task<bool> SendResetPasswordEmail(string email, string? resetPasswordLink)
    {
        SmtpClient client = new(_smtpSettings.Server, _smtpSettings.Port)
        {
            EnableSsl = true,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(_smtpSettings.SenderEmail, _smtpSettings.Password)
        };

        string subject = "Reset Your Password";
        string templatePath = ".\\Views\\Authentication\\ResetPasswordTemplate.cshtml";
        string message = await File.ReadAllTextAsync(templatePath);

        message = message.Replace("{{resetPasswordLink}}", resetPasswordLink);
        
        try
        {
            MailMessage mailMessage = new()
            {
                From = new MailAddress(_smtpSettings.SenderEmail),
                To = { email },
                Subject = subject,
                Body = message,
                IsBodyHtml = true
            };

            await client.SendMailAsync(mailMessage);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

}
