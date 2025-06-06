using System.Net;
using System.Net.Mail;
using ElasticFind.Repository.ViewModels;
using ElasticFind.Service.Interfaces;
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
        Console.WriteLine("Smtp SenderEmail: " + _smtpSettings.SenderEmail);
        Console.WriteLine("Smtp Server: " + _smtpSettings.Server);
        Console.WriteLine("Smtp Port: " + _smtpSettings.Port);
        Console.WriteLine("Smtp Password: " + _smtpSettings.Password);
        SmtpClient client = new(_smtpSettings.Server, _smtpSettings.Port)
        {
            EnableSsl = true,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(_smtpSettings.SenderEmail, _smtpSettings.Password)
        };

        string subject = "Reset Your Password";
        string templatePath = "D:\\Tatva\\ElasticFind\\ElasticFind.Web\\Views\\Authentication\\ResetPasswordTemplate.cshtml";
        Console.WriteLine($"Template path: {templatePath}");
        string message = await File.ReadAllTextAsync(templatePath);

        message = message.Replace("{{resetPasswordLink}}", resetPasswordLink);
        Console.WriteLine("Message after link replace: " + message);
        
        try
        {
            Console.WriteLine("Sender Email: " + _smtpSettings.SenderEmail);
            MailMessage mailMessage = new()
            {
                From = new MailAddress(_smtpSettings.SenderEmail),
                To = { email },
                Subject = subject,
                Body = message,
                IsBodyHtml = true
            };
            Console.WriteLine("From: " + mailMessage.From);
            Console.WriteLine("To: " + mailMessage.To);

            await client.SendMailAsync(mailMessage);
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine("Exception in sending email: " + e.Message);
            Console.WriteLine("Exception: " + e);
            return false;
        }
    }

}
