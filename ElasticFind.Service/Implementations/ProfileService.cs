using ElasticFind.Repository.Data;
using ElasticFind.Repository.Interfaces;
using ElasticFind.Repository.ViewModels;
using ElasticFind.Service.Interfaces;

namespace ElasticFind.Service.Implementations;

public class ProfileService : IProfileService
{
    private readonly IUserRepository _userRepository;
    public ProfileService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<JsonResponse> ChangePassword(ChangePasswordViewModel viewModel)
    {
        User? user = await _userRepository.GetUserByEmail(viewModel.Email);
        if (user == null)
        {
            return new JsonResponse { Success = false, Message = "User with this ID was not found!" };
        }

        if (user.Password != viewModel.CurrentPassword)
        {
            return new JsonResponse { Success = false, Message = "Your current password does not match! Please enter correct current password!" };
        }

        //Change User Password
        user.Password = viewModel.NewPassword;

        bool isUserUpdated = await _userRepository.UpdateUser(user);
        if (isUserUpdated)
        {
            return new JsonResponse { Success = true, Message = "Your password has been changed successfully!" };
        }
        else
        {
            return new JsonResponse { Success = false, Message = "Some error occured while changing your password!" };
        }
    }

    public async Task<MyProfileViewModel?> GetProfileByEmail(string email)
    {
        User? user = await _userRepository.GetUserByEmail(email);
        if (user == null)
        {
            Console.WriteLine("Error: User with this ID was not found!");
            return null;
        }

        MyProfileViewModel profileViewModel = new()
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Username = user.Username,
            Email = user.Email,
            ProfileImagePath = user.ProfileImage,
            Phone = user.Phone,
            Role = user.Role.RoleName
        };

        return profileViewModel;
    }

    public async Task<JsonResponse> UpdateProfile(MyProfileViewModel myProfileViewModel)
    {
        User? existingUser = await _userRepository.GetUserByUsernameOrPhone(myProfileViewModel.Username, myProfileViewModel.Phone);
        if (existingUser != null)
        {
            return new JsonResponse { Success = false, Message = "User by this username already exists! Please select different username." };
        }


    }
}
