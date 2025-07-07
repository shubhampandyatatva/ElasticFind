using ElasticFind.Repository.Data;
using ElasticFind.Repository.Interfaces;
using ElasticFind.Repository.ViewModels;
using ElasticFind.Service.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace ElasticFind.Service.Implementations;

public class ProfileService : IProfileService
{
    private readonly IUserRepository _userRepository;
    private readonly IUploadImageService _uploadImageService;
    public ProfileService(IUserRepository userRepository, IUploadImageService uploadImageService)
    {
        _userRepository = userRepository;
        _uploadImageService = uploadImageService;
    }

    public async Task<JsonResponse> ChangePassword(ChangePasswordViewModel viewModel)
    {
        User? user = await _userRepository.GetUserByEmail(viewModel.Email);
        if (user == null)
        {
            return new JsonResponse { Success = false, Message = "User with this ID was not found!" };
        }
        var hasher = new PasswordHasher<User>();
        var result = hasher.VerifyHashedPassword(user, user.Password, viewModel.CurrentPassword);
        if (result  != PasswordVerificationResult.Success)
        {
            return new JsonResponse { Success = false, Message = "Your current passwords do not match! Please enter correct current password!" };
        }

        //Change User Password
        string hashedPassword = hasher.HashPassword(user, viewModel.NewPassword);
        user.Password = hashedPassword;

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
        string? occupiedField = await _userRepository.GetOccupiedField(myProfileViewModel.Username, myProfileViewModel.Phone, (int)myProfileViewModel.Id);
        if (!string.IsNullOrEmpty(occupiedField))
        {
            return new JsonResponse { Success = false, Message = $"User with this {occupiedField} already exists! Please enter different {occupiedField}." };
        }

        User? user = await _userRepository.GetUserByEmail(myProfileViewModel.Email);
        if (user == null)
        {
            Console.WriteLine("Error: User with this email was not found!");
            return new JsonResponse { Success = false, Message = "Some error occured in updating profile!" };
        }
        if (myProfileViewModel.ProfileImage != null)
        {
            string[] allowedExtensions = { ".jpg", ".jpeg", ".png", ".svg" };
            string fileExtension = Path.GetExtension(myProfileViewModel.ProfileImage.FileName).ToLower();

            if (!allowedExtensions.Contains(fileExtension))
            {
                return new JsonResponse { Success = false, Message = "Selected type for image is not acceptable. Please select jpg, png or svg file." };
            }
        }
        string? imagePath = await _uploadImageService.UploadImage(myProfileViewModel.ProfileImage);
        Console.WriteLine("Image Path: " + imagePath);

        //Change user information
        user.FirstName = myProfileViewModel.FirstName;
        user.LastName = myProfileViewModel.LastName;
        user.Username = myProfileViewModel.Username;
        user.Phone = myProfileViewModel.Phone;
        user.ProfileImage = imagePath;

        bool isProfileUpdated = await _userRepository.UpdateUser(user);
        if (isProfileUpdated)
        {
            return new JsonResponse { Success = true, Message = "Your profile has been updated successfully!", Anonymous = imagePath };
        }
        else
        {
            return new JsonResponse { Success = false, Message = "Some error occured in updating your profile!" };
        }
    }
}
