using ElasticFind.Repository.Data;
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

    public async Task<bool> DeleteUser(int id)
    {
        User? user = await _userRepository.GetUserById(id);
        if (user == null)
        {
            Console.WriteLine("Error: User not found with ID: " + id);
            return false;
        }

        user.Isdeleted = true;
        bool result = await _userRepository.UpdateUser(user);
        if (!result)
        {
            Console.WriteLine("Error: Failed to delete user with ID: " + id);
            return false;
        }
        return true;
    }

    public async Task<bool> ToggleUserStatus(int id)
    {
        User? user = await _userRepository.GetUserById(id);
        if (user == null)
        {
            Console.WriteLine("Error: User not found with ID: " + id);
            return false;
        }

        user.Isactive = !user.Isactive.GetValueOrDefault();
        bool result = await _userRepository.UpdateUser(user);
        if (!result)
        {
            Console.WriteLine("Error: Failed to delete user with ID: " + id);
            return false;
        }
        return true;
    }

}
