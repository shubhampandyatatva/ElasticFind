using ElasticFind.Repository.ViewModels;

namespace ElasticFind.Service.Interfaces;

public interface IUserService
{
    Task<DisplayUsersViewModel> GetUserList(int page, int pageSize, string? searchString, string sortOrder);

}
