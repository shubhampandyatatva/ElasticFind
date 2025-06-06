
namespace ElasticFind.Service.Interfaces;

public interface IEmailService
{
    Task<bool> SendResetPasswordEmail(string email, string? resetPasswordLink);

}
