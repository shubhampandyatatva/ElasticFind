using ElasticFind.Repository.Interfaces;
using ElasticFind.Repository.ViewModels;
using ElasticFind.Service.Interfaces;

namespace ElasticFind.Service.Implementations;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<DisplayUsersViewModel> GetUserList(int page, int pageSize, string? searchString, string sortOrder)
    {
        PaginationViewModel paginationViewModel = new()
        {
            Page = page,
            PageSize = pageSize,
            SearchString = searchString,
            SortOrder = sortOrder
        };
        List<UserViewModel> usersList = _userRepository.GetUserList(paginationViewModel);
        int totalRecords = searchString == null ? await _userRepository.GetTotalUsers() : await _userRepository.GetTotalSearchedUsers(searchString);
        paginationViewModel.TotalRecords = totalRecords;

        DisplayUsersViewModel viewModel = new()
        {
            PaginationViewModel = paginationViewModel,
            UserList = usersList
        };

        return viewModel;
    }

}
