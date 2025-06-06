using ElasticFind.Repository.Data;
using ElasticFind.Repository.ViewModels;

namespace ElasticFind.Repository.Interfaces;

public interface IAuthRepository
{
    Task<string> CheckIfEmailUsernameAndPhoneExists(string email, string username, string phone);

    Task<bool> CheckIfUserByEmailExists(string email);
    Task<bool> CheckUserPassword(string email, string password);
    Task<User?> GetUserByEmail(string email);

    Task<bool> RegisterUser(User user);

}
