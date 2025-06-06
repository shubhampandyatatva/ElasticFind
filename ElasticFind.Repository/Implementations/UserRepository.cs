using ElasticFind.Repository.Data;
using ElasticFind.Repository.Interfaces;

namespace ElasticFind.Repository.Implementations;

public class UserRepository : IUserRepository
{
    private readonly ElasticFindContext _dbcontext;
    public UserRepository(ElasticFindContext dbcontext)
    {
        _dbcontext = dbcontext;
    }

    public async Task<bool> AddUser(User user)
    {
        try
        {
            await _dbcontext.Users.AddAsync(user);
            await _dbcontext.SaveChangesAsync();
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine("Exception while adding user: " + e.Message);
            Console.WriteLine("Exception: " + e);
            return false;
        }
    }

    public async Task<bool> UpdateUser(User user)
    {
        try
        {
            _dbcontext.Users.Update(user);
            await _dbcontext.SaveChangesAsync();
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine("Exception while updating user: " + e.Message);
            Console.WriteLine("Exception: " + e);
            return false;
        }
    }
}
