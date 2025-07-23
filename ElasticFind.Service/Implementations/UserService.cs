using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using ElasticFind.Repository.Data;
using ElasticFind.Repository.Interfaces;
using ElasticFind.Repository.ViewModels;
using ElasticFind.Service.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ElasticFind.Service.Implementations;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IDataProtector _dataProtector;
    private readonly IAuthRepository _authRepository;
    private readonly IConfiguration _configuration;
    public UserService(IUserRepository userRepository, IDataProtectionProvider dataProtectionProvider, IAuthRepository authRepository, IConfiguration configuration)
    {
        _userRepository = userRepository;
        _dataProtector = dataProtectionProvider.CreateProtector("ResetPasswordProtector");
        _authRepository = authRepository;
        _configuration = configuration;
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

    public async Task<DisplayFilesViewModel> GetFiles(PaginationViewModel pagination)
    {
        List<FileViewModel> files = await _userRepository.GetFiles(pagination);
        int totalRecords = pagination.SearchString != null ? await _userRepository.GetTotalFilesBySearchString(pagination.SearchString) : await _userRepository.GetTotalFiles();
        pagination.TotalRecords = totalRecords;

        DisplayFilesViewModel viewModel = new()
        {
            Pagination = pagination,
            Files = files
        };

        return viewModel;
    }

    public async Task<User?> GetUserByEmail(string email)
    {
        return await _userRepository.GetUserByEmail(email);
    }

    public async Task<string> GetUserFullNameById(string? uploadedBy)
    {
        if (!int.TryParse(uploadedBy, out int uploadedByInt))
        {
            Console.WriteLine("Error: Invalid user ID format: " + uploadedBy);
            return string.Empty;
        }
        User? user = await _userRepository.GetUserById(uploadedByInt);
        if (user == null)
        {
            Console.WriteLine("Error: User not found with ID: " + uploadedBy);
            return string.Empty;
        }
        return $"{user.FirstName} {user.LastName}";
    }

    public async Task<string?> UploadImage(IFormFile? image)
    {
        string? imagePath;
        if (image == null)
        {
            Console.WriteLine("Error in Upload Service: Provided image is null.");
            return null;
        }
        string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        imagePath = Path.Combine("uploads", Guid.NewGuid() + Path.GetExtension(image.FileName));
        using (var fileStream = new FileStream(Path.Combine("wwwroot", imagePath), FileMode.Create))
        {
            await image.CopyToAsync(fileStream);
        }

        return imagePath;
    }

    public string GenerateResetPasswordToken(string email)
    {
        DateTime tokenExpiryDate = DateTime.UtcNow.AddHours(24); // Token expires after 24 hours
        string tokenData = $"{email}|{tokenExpiryDate.Ticks}";
        Console.WriteLine("Token Data: " + tokenData);
        return _dataProtector.Protect(tokenData);
    }

    public async Task<JsonResponse> ValidateResetPasswordToken(string token)
    {
        string decryptedToken;
        try
        {
            decryptedToken = _dataProtector.Unprotect(token);  //decrypt the token
        }
        catch (Exception e)
        {
            Console.WriteLine("Exception Message: " + e.Message);
            Console.WriteLine("Exception while decrypting token: " + e);
            return new JsonResponse { Success = false, Message = "You do not have a valid reset password token!" };
        }

        var tokenParts = decryptedToken.Split('|');   //token has {email} | {expiryticks}
        if (tokenParts.Length != 2 || !long.TryParse(tokenParts[1].Trim(), out long tokenExpiryTicks))
        {
            return new JsonResponse { Success = false, Message = "Invalid Reset Password Token!" };
        }

        DateTime tokenExpiryDate = new(tokenExpiryTicks, DateTimeKind.Utc);   //converts expiry ticks into datetime object
        if (tokenExpiryDate < DateTime.UtcNow)
        {
            return new JsonResponse { Success = false, Message = "Your Reset Password token has expired!" };
        }

        string email = tokenParts[0].Trim();
        User? user = await _authRepository.GetUserByEmail(email);
        if (user == null)
        {
            return new JsonResponse { Success = false, Message = "User with this token was not found!" };
        }
        return new JsonResponse { Success = true, Anonymous = email };
    }

    public string DecryptResetPasswordToken(string token)
    {
        try
        {
            return _dataProtector.Unprotect(token);  //return the decrypted the token
        }
        catch (Exception e)
        {
            Console.WriteLine("Exception Message: " + e.Message);
            Console.WriteLine("Exception while decrypting token: " + e);
            return string.Empty;
        }
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
        if (result != PasswordVerificationResult.Success)
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
        string? imagePath = await UploadImage(myProfileViewModel.ProfileImage);
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
    
    public string GenerateJwtToken(User user)
    {
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
        SigningCredentials credentials = new(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.RoleName),
            new Claim(ClaimTypes.Name, user.Username),
        };
        // Console.WriteLine(user.RememberMe);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }


    public string GenerateJwtTokenForOnlyOffice(object payload)
    {
        var secret = _configuration["OnlyOffice:JwtSecret"];
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var handler = new JwtSecurityTokenHandler();
        var token = new JwtSecurityToken(
            claims: new[] { new Claim(ClaimTypes.UserData, Newtonsoft.Json.JsonConvert.SerializeObject(payload)) },
            signingCredentials: creds
        );

        return handler.WriteToken(token);
    }

    public string? GetClaimValue(string jwtToken, string claimType)
    {
        ClaimsPrincipal? claimsPrincipal = ValidateToken(jwtToken);  // Verify if the token is valid
        if (claimsPrincipal == null)
        {
            Console.WriteLine("Error: JWT Token is null!");
            return null;
        }

        Claim? claim = claimsPrincipal.Claims.FirstOrDefault(c => c.Type == claimType);

        return claim?.Value;
    }

    private static ClaimsPrincipal? ValidateToken(string token)
    {
        JwtSecurityTokenHandler handler = new();
        JwtSecurityToken? jsonToken = handler.ReadToken(token) as JwtSecurityToken;

        // If token is valid, then return ClaimsPrincipal
        if (jsonToken != null)
        {
            return new ClaimsPrincipal(new ClaimsIdentity(jsonToken.Claims));
        }

        return null;
    }
}
