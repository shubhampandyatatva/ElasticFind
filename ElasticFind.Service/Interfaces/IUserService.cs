using ElasticFind.Repository.ViewModels;

namespace ElasticFind.Service.Interfaces;

public interface IUserService
{
    Task<bool> DeleteUser(int id);
    Task<DisplayFilesViewModel> GetFiles(PaginationViewModel pagination);
    Task<DisplayUsersViewModel> GetUserList(int page, int pageSize, string? searchString, string sortOrder);
    Task<bool> ToggleUserStatus(int id);
}
