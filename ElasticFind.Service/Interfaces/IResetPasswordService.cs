
using ElasticFind.Repository.ViewModels;

namespace ElasticFind.Service.Interfaces;

public interface IResetPasswordService
{
    string DecryptResetPasswordToken(string token);

    string GenerateResetPasswordToken(string email);
    Task<JsonResponse> ValidateResetPasswordToken(string token);
}
