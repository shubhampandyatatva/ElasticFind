using System.Threading.Tasks;
using ElasticFind.Repository.Data;
using ElasticFind.Repository.Interfaces;
using ElasticFind.Repository.ViewModels;
using Microsoft.AspNetCore.Identity;
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

        bool isUserActive = await _userRepository.IsUserActive(email);
        if (!isUserActive)
        {
            return new JsonResponse { Success = false, Message = "Your status is currently inactive. Please contact admin to activate your status." };
        }

        // bool isPasswordValid = await _authRepository.CheckUserPassword(email, password);
        User? user = await _authRepository.GetUserByEmail(email);
        if (user == null)
        {
            Console.WriteLine("Error: User with this email was not found in database!");
            return new JsonResponse { Success = false, Message = "Some error occured" };
        }
        var hasher = new PasswordHasher<User>();
        var result = hasher.VerifyHashedPassword(user, user.Password, password);
        if (result != PasswordVerificationResult.Success)
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

        var hasher = new PasswordHasher<User>();

        User user = new()
        {
            FirstName = registerViewModel.FirstName,
            LastName = registerViewModel.LastName,
            Username = registerViewModel.Username,
            Email = registerViewModel.Email,
            Phone = registerViewModel.Phone,
            RoleId = 2,
            Isdeleted = false,
            Isactive = true
        };

        string hashedPassword = hasher.HashPassword(user, registerViewModel.Password);
        Console.WriteLine("Hashed Password: " + hashedPassword);
        user.Password = hashedPassword;

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
        // string email = tokenParts[0].Trim();
        string email = "tatva.pcs90@outlook.com";

        User? user = await _authRepository.GetUserByEmail(email);
        if (user == null)
        {
            Console.WriteLine("Error: User with this email was not found in database!");
            return false;
        }

        var hasher = new PasswordHasher<User>();
        string hashedPassword = hasher.HashPassword(user, resetPasswordViewModel.NewPassword);
        user.Password = hashedPassword;

        bool isPasswordUpdated = await _userRepository.UpdateUser(user);

        return isPasswordUpdated;
    }

    public async Task<User?> GetUserByEmail(string email)
    {
        return await _authRepository.GetUserByEmail(email);
    }

}
