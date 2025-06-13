using ElasticFind.Repository.Data;
using ElasticFind.Repository.Interfaces;
using ElasticFind.Repository.ViewModels;
using ElasticFind.Service.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace ElasticFind.Service.Implementations;

public class ResetPasswordService : IResetPasswordService
{
    private readonly IDataProtector _dataProtector;
    private readonly IAuthRepository _authRepository;
    public ResetPasswordService(IDataProtectionProvider dataProtectionProvider, IAuthRepository authRepository)
    {
        _dataProtector = dataProtectionProvider.CreateProtector("ResetPasswordProtector");
        _authRepository = authRepository;
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
        if(user == null)
        {
            return new JsonResponse { Success = false, Message = "User with this token was not found!" };
        }
        return new JsonResponse {Success = true, Anonymous = email};
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
}
