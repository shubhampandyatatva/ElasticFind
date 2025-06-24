using ElasticFind.Repository.Data;
using ElasticFind.Repository.ViewModels;

namespace ElasticFind.Service.Interfaces;

public interface IUserService
{
    Task<bool> DeleteUser(int id);
    Task<DisplayFilesViewModel> GetFiles(PaginationViewModel pagination);
    Task<User?> GetUserByEmail(string email);
    Task<string> GetUserFullNameById(string? uploadedBy);
    Task<DisplayUsersViewModel> GetUserList(int page, int pageSize, string? searchString, string sortOrder);
    Task<bool> ToggleUserStatus(int id);
}
