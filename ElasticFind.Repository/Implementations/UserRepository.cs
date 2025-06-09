using ElasticFind.Repository.Data;
using ElasticFind.Repository.Interfaces;
using Microsoft.EntityFrameworkCore;

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

    public Task<User?> GetUserByEmail(string email)
    {
        return _dbcontext.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<string?> GetUserByUsername(string username, string phone)
    {
        User? user = await _dbcontext.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower() || u.Phone == phone);
        if (user == null)
        {
            return string.Empty;
        }
        if (user.Username.ToLower() == username)
        {
            return "username";
        }
        if (user.Phone == phone)
        {
            return "phone number";
        }
        
    }

}
