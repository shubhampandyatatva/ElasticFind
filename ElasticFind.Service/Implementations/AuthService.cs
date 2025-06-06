using System.Threading.Tasks;
using ElasticFind.Repository.Data;
using ElasticFind.Repository.Interfaces;
using ElasticFind.Repository.ViewModels;
using ElasticFind.Service.Interfaces;

namespace ElasticFind.Service.Implementations;

public class AuthService : IAuthService
{
    private readonly IAuthRepository _authRepository;
    private readonly IResetPasswordService _resetPasswordService;
    private readonly IUserRepository _userRepository;
    public AuthService(IAuthRepository authRepository, IResetPasswordService resetPasswordService, IUserRepository userRepository)
    {
        _authRepository = authRepository;
        _resetPasswordService = resetPasswordService;
        _userRepository = userRepository;
    }

    public async Task<JsonResponse> ValidateUser(string email, string password)
    {
        bool checkIfUserByEmailExists = await _authRepository.CheckIfUserByEmailExists(email);
        if (!checkIfUserByEmailExists)
        {
            return new JsonResponse { Success = false, Message = "User with this email does not exist! Please register to continue" };
        }

        bool isPasswordValid = await _authRepository.CheckUserPassword(email, password);
        if (!isPasswordValid)
        {
            return new JsonResponse { Success = false, Message = "Invalid Email or Password!" };
        }
        return new JsonResponse { Success = true, Message = "Logged In Successfully!" };
    }

    public async Task<JsonResponse> RegisterUser(RegisterViewModel registerViewModel)
    {
        string existingField = await _authRepository.CheckIfEmailUsernameAndPhoneExists(registerViewModel.Email, registerViewModel.Username, registerViewModel.Phone);
        if (!string.IsNullOrEmpty(existingField))
        {
            return new JsonResponse { Success = false, Message = $"User with this {existingField} already exists! Please select a different {existingField}" };
        }

        User user = new()
        {
            FirstName = registerViewModel.FirstName,
            LastName = registerViewModel.LastName,
            Username = registerViewModel.Username,
            Email = registerViewModel.Email,
            Phone = registerViewModel.Phone,
            RoleId = 2,
            Password = registerViewModel.Password
        };
        bool isUserRegistered = await _authRepository.RegisterUser(user);

        if (!isUserRegistered)
        {
            return new JsonResponse { Success = false, Message = "Some error occurred while registeration!" };
        }
        return new JsonResponse { Success = true, Message = "Registered successfully!" };
    }

    public async Task<bool> CheckIfUserExistsByEmail(string email)
    {
        return await _authRepository.CheckIfUserByEmailExists(email);
    }

    public async Task<bool> ResetUserPassword(ResetPasswordViewModel resetPasswordViewModel)
    {
        string decryptedToken = _resetPasswordService.DecryptResetPasswordToken(resetPasswordViewModel.Token);
        if (string.IsNullOrEmpty(decryptedToken))
        {
            Console.WriteLine("Error: Decrypted Token is null!");
            return false;
        }
        string[] tokenParts = decryptedToken.Split('|');
        string email = tokenParts[0].Trim();

        User? user = await _authRepository.GetUserByEmail(email);
        if (user == null)
        {
            Console.WriteLine("Error: User with this email was not found in database!");
            return false;
        }

        // Replace current user password with new password

        user.Password = resetPasswordViewModel.NewPassword;

        bool isPasswordUpdated = await _userRepository.UpdateUser(user);

        return isPasswordUpdated;
    }
}
