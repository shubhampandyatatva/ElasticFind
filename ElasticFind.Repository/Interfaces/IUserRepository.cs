using ElasticFind.Repository.Data;

namespace ElasticFind.Repository.Interfaces;

public interface IUserRepository
{
    Task<bool> AddUser(User user);
    Task<bool> UpdateUser(User user);
    Task<User?> GetUserByEmail(string email);
    Task<string?> GetUserByUsername(string username, string phone);

}
