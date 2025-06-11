using ElasticFind.Repository.Data;
using ElasticFind.Repository.Interfaces;
using ElasticFind.Repository.ViewModels;
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
        return _dbcontext.Users.Include(u => u.Role).Where(u => u.Isdeleted != true).FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<string?> GetOccupiedField(string username, string phone, int id)
    {
        User? user = await _dbcontext.Users.Where(u => u.Isdeleted != true).FirstOrDefaultAsync(u => (u.Username.ToLower() == username.ToLower() || u.Phone == phone) && u.Id != id);
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
        return string.Empty;
    }

    public async Task<int> GetTotalUsers()
    {
        return await _dbcontext.Users.Where(u => u.Isdeleted != true).CountAsync();
    }

    public async Task<int> GetTotalSearchedUsers(string searchString)
    {
        return await _dbcontext.Users.Where(u => u.Isdeleted != true && (u.FirstName.ToLower().Contains(searchString.ToLower()) || u.LastName.ToLower().Contains(searchString.ToLower()) || u.Email.ToLower().Contains(searchString.ToLower()) || u.Phone.ToLower().Contains(searchString.ToLower()))).CountAsync();
    }

    public List<UserViewModel> GetUserList(PaginationViewModel paginationViewModel)
    {
        var query = _dbcontext.Users.Where(u => u.Isdeleted != true).OrderBy(u => u.Id);
        if (!string.IsNullOrEmpty(paginationViewModel.SearchString))
        {
            query = query.Where(u => u.FirstName.ToLower().Contains(paginationViewModel.SearchString.ToLower()) || u.LastName.ToLower().Contains(paginationViewModel.SearchString.ToLower()) || u.Email.ToLower().Contains(paginationViewModel.SearchString.ToLower()) || u.Phone.ToLower().Contains(paginationViewModel.SearchString.ToLower())).OrderBy(u => u.Id);
        }

        query = paginationViewModel.SortOrder == "Asc" ? query.OrderBy(u => u.FirstName) : query.OrderByDescending(u => u.FirstName);

        List<UserViewModel> users = query.Skip((paginationViewModel.Page - 1) * paginationViewModel.PageSize).Take(paginationViewModel.PageSize).Select(u => new UserViewModel
        {
            Id = u.Id,
            Profileimage = u.ProfileImage,
            Firstname = u.FirstName,
            Lastname = u.LastName,
            Email = u.Email,
            Phone = u.Phone,
            IsActive = u.Isactive == true ? "Active" : "Inactive",
        }).ToList();

        return users;
    }

    public Task<User?> GetUserById(int id)
    {
        return _dbcontext.Users.Include(u => u.Role).Where(u => u.Isdeleted != true).FirstOrDefaultAsync(u => u.Id == id);
    }
}
