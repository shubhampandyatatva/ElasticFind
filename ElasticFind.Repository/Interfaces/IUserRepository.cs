using ElasticFind.Repository.Data;

namespace ElasticFind.Repository.Interfaces;

public interface IUserRepository
{
    Task<bool> AddUser(User user);
    Task<bool> UpdateUser(User user);
}
