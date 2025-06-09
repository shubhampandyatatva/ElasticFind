using ElasticFind.Repository.ViewModels;

namespace ElasticFind.Service.Interfaces;

public interface IProfileService
{
    Task<JsonResponse> ChangePassword(ChangePasswordViewModel viewModel);
    Task<MyProfileViewModel?> GetProfileByEmail(string email);
    Task<JsonResponse> UpdateProfile(MyProfileViewModel myProfileViewModel);

}
