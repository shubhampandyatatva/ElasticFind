using System.Threading.Tasks;
using ElasticFind.Repository.Data;
using ElasticFind.Repository.Interfaces;
using ElasticFind.Repository.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace ElasticFind.Repository.Implementations;

public class AuthRepository : IAuthRepository
{
    private readonly ElasticFindContext _dbcontext;
    public AuthRepository(ElasticFindContext dbcontext)
    {
        _dbcontext = dbcontext;
    }

    public async Task<bool> CheckIfUserByEmailExists(string email)
    {
        User? user = await _dbcontext.Users.FirstOrDefaultAsync(u => u.Email == email);
        return user != null;
    }

    public async Task<bool> CheckUserPassword(string email, string password)
    {
        User? user = await _dbcontext.Users.FirstOrDefaultAsync(u => u.Email == email);
        return user != null && user.Password == password;
    }

    public async Task<string> CheckIfEmailUsernameAndPhoneExists(string email, string username, string phone)
    {
        // User? user = await _dbcontext.Users.FirstOrDefaultAsync(u => (u.Email.ToLower() == email.ToLower() || u.Username.ToLower() == username.ToLower() || u.Phone.ToLower() == phone.ToLower()) && u.RoleId == 2);
        User? user = await _dbcontext.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower() || u.Username.ToLower() == username.ToLower() || u.Phone.ToLower() == phone.ToLower());
        if (user == null)
        {
            return string.Empty;
        }
        if (user.Email.ToLower() == email.ToLower())
        {
            return "email";
        }
        if (user.Username.ToLower() == username.ToLower())
        {
            return "username";
        }
        if (user.Phone.ToLower() == phone.ToLower())
        {
            return "phone number";
        }
        return string.Empty;
    }

    public async Task<bool> RegisterUser(User user)
    {
        try
        {
            await _dbcontext.Users.AddAsync(user);
            await _dbcontext.SaveChangesAsync();
            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine("Exception while registering user: " + e.Message);
            Console.WriteLine("Exception: " + e);
            return false;
        }
    }

    public async Task<User?> GetUserByEmail(string email)
    {
        return await _dbcontext.Users.FirstOrDefaultAsync(u => u.Email == email);
    }
}
