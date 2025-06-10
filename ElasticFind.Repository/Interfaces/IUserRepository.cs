using ElasticFind.Repository.Data;
using ElasticFind.Repository.ViewModels;

namespace ElasticFind.Repository.Interfaces;

public interface IUserRepository
{
    Task<bool> AddUser(User user);
    Task<bool> UpdateUser(User user);
    Task<User?> GetUserByEmail(string email);
    Task<string?> GetOccupiedField(string username, string phone, int id);
    Task<int> GetTotalUsers();
    Task<int> GetTotalSearchedUsers(string searchString);
    List<UserViewModel> GetUserList(PaginationViewModel paginationViewModel);
}
