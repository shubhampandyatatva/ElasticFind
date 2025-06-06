using ElasticFind.Repository.Data;
using ElasticFind.Repository.ViewModels;

namespace ElasticFind.Service.Interfaces;

public interface IAuthService
{
    Task<bool> CheckIfUserExistsByEmail(string email);
    Task<User?> GetUserByEmail(string email);
    Task<JsonResponse> RegisterUser(RegisterViewModel registerViewModel);
    Task<bool> ResetUserPassword(ResetPasswordViewModel resetPasswordViewModel);
    Task<JsonResponse> ValidateUser(string email, string password);
}
