using ElasticFind.Repository.Data;
using ElasticFind.Repository.ViewModels;
using Microsoft.AspNetCore.Http;

namespace ElasticFind.Service.Interfaces;

public interface IUserService
{
    Task<bool> DeleteUser(int id);
    Task<DisplayFilesViewModel> GetFiles(PaginationViewModel pagination);
    Task<User?> GetUserByEmail(string email);
    Task<string> GetUserFullNameById(string? uploadedBy);
    Task<DisplayUsersViewModel> GetUserList(int page, int pageSize, string? searchString, string sortOrder);
    Task<bool> ToggleUserStatus(int id);
    Task<string?> UploadImage(IFormFile? image);
    string DecryptResetPasswordToken(string token);
    string GenerateResetPasswordToken(string email);
    Task<JsonResponse> ValidateResetPasswordToken(string token);
    Task<JsonResponse> ChangePassword(ChangePasswordViewModel viewModel);
    Task<MyProfileViewModel?> GetProfileByEmail(string email);
    Task<JsonResponse> UpdateProfile(MyProfileViewModel myProfileViewModel);
    string GenerateJwtToken(User user);
    string GenerateJwtTokenForOnlyOffice(object payload);
    string? GetClaimValue(string jwtToken, string claimType);
}
