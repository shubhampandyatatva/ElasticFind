using System.Security.Claims;
using System.Threading.Tasks;
using ElasticFind.Repository.Data;
using ElasticFind.Repository.ViewModels;
using ElasticFind.Service.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ElasticFind.Web.Controllers;

public class AuthenticationController : Controller
{
    private readonly IAuthService _authService;
    private readonly IResetPasswordService _resetPasswordService;
    private readonly IEmailService _emailService;
    private readonly IJwtService _jwtService;
    public AuthenticationController(IAuthService authService, IResetPasswordService resetPasswordService, IEmailService emailService, IJwtService jwtService)
    {
        _authService = authService;
        _resetPasswordService = resetPasswordService;
        _emailService = emailService;
        _jwtService = jwtService;
    }

    [HttpGet]
    public async Task<IActionResult> Login()
    {
        string? jwtToken = Request.Cookies["JwtToken"];
        if (jwtToken == null)
        {
            return View();
        }

        string? email = _jwtService.GetClaimValue(jwtToken, ClaimTypes.Email);
        if (email == null)
        {
            Console.WriteLine("Error: Cannot read email from JWTToken!");
            return View();
        }
        User? existingUser = await _authService.GetUserByEmail(email);
        if (existingUser == null)
        {
            return View();
        }

        string? roleId = _jwtService.GetClaimValue(jwtToken, ClaimTypes.Role);
        if (roleId == null)
        {
            Console.WriteLine("Error: Cannot read roleId from JWTToken!");
            return View();
        }

        // Set the profile image path in cookies if it exists
        if (existingUser.ProfileImage != null)
        {
            CookieOptions cookieOptions = new()
            {
                HttpOnly = true, // Prevent JavaScript access
                Secure = true, // Ensure cookie is only sent over HTTPS
                Expires = DateTime.UtcNow.AddDays(7)
            };
            Response.Cookies.Append("ProfileImagePath", existingUser.ProfileImage, cookieOptions);
        }

        return roleId == "1" ? RedirectToAction("Index", "Home") : RedirectToAction("Index", "UserDashboard");
    }

    [HttpPost]
    public async Task<IActionResult> Login([FromForm] LoginViewModel loginViewModel)
    {
        if (ModelState.IsValid)
        {
            JsonResponse response = await _authService.ValidateUser(loginViewModel.Email, loginViewModel.Password);
            if (response.Success)
            {
                // create JWT Token for that user and save it in cookies

                User? user = await _authService.GetUserByEmail(loginViewModel.Email);
                if (user == null)
                {
                    Console.WriteLine("Error: User not found by this ID!");
                    TempData["ErrorMessage"] = "Some error occured!";
                    return View(loginViewModel);
                }
                string token = _jwtService.GenerateJwtToken(user);

                if (!loginViewModel.RememberMe)
                {
                    CookieOptions cookieOptions = new()
                    {
                        HttpOnly = true, // Prevent JavaScript access
                        Secure = true, // Ensure cookie is only sent over HTTPS
                        Expires = DateTime.UtcNow.AddHours(24)
                    };
                    Response.Cookies.Append("JwtToken", token, cookieOptions);
                }
                else
                {
                    CookieOptions cookieOptions = new()
                    {
                        HttpOnly = true,
                        Secure = true,
                        Expires = DateTime.UtcNow.AddDays(7)
                    };
                    Response.Cookies.Append("JwtToken", token, cookieOptions);
                }

                // Set the profile image path in cookies if it exists
                if (user.ProfileImage != null)
                {
                    CookieOptions cookieOptions = new()
                    {
                        HttpOnly = true, // Prevent JavaScript access
                        Secure = true, // Ensure cookie is only sent over HTTPS
                        Expires = DateTime.UtcNow.AddDays(7)
                    };
                    Response.Cookies.Append("ProfileImagePath", user.ProfileImage, cookieOptions);
                }

                TempData["SuccessMessage"] = response.Message;

                return user.RoleId == 1 ? RedirectToAction("Index", "Home") : RedirectToAction("Index", "UserDashboard");
            }
            else
            {
                TempData["ErrorMessage"] = response.Message;
                return View(loginViewModel);
            }
        }
        else
        {
            TempData["ErrorMessage"] = "Some error occured!";
            return View(loginViewModel);
        }
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Register([FromForm] RegisterViewModel registerViewModel)
    {
        if (ModelState.IsValid)
        {
            JsonResponse response = await _authService.RegisterUser(registerViewModel);
            if (response.Success)
            {
                // create JWT Token for that user and save it in cookies

                User? user = await _authService.GetUserByEmail(registerViewModel.Email);
                if (user == null)
                {
                    Console.WriteLine("Error: User not found by this ID!");
                    TempData["ErrorMessage"] = "Some error occured!";
                    return View(registerViewModel);
                }
                string token = _jwtService.GenerateJwtToken(user);

                CookieOptions cookieOptions = new()
                {
                    HttpOnly = true, // Prevent JavaScript access
                    Secure = true, // Ensure cookie is only sent over HTTPS
                    Expires = DateTime.UtcNow.AddHours(24)
                };
                Response.Cookies.Append("JwtToken", token, cookieOptions);

                TempData["SuccessMessage"] = response.Message;
                return RedirectToAction("Index", "UserDashboard");
            }
            else
            {
                TempData["ErrorMessage"] = response.Message;
                return View(registerViewModel);
            }
        }
        else
        {
            TempData["ErrorMessage"] = "Some error occurred!";
            return View(registerViewModel);
        }
    }

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> ForgotPassword([FromForm] ForgotPasswordViewModel forgotPasswordViewModel)
    {
        if (ModelState.IsValid)
        {
            bool userExists = await _authService.CheckIfUserExistsByEmail(forgotPasswordViewModel.Email);
            if (!userExists)
            {
                TempData["ErrorMessage"] = "User with this email does not exist. Please enter a valid email or register on our website.";
                return View(forgotPasswordViewModel);
            }
            string resetPasswordToken = _resetPasswordService.GenerateResetPasswordToken(forgotPasswordViewModel.Email);
            string? resetPasswordLink = Url.Action("ResetPassword", "Authentication", new { token = resetPasswordToken }, Request.Scheme); //watch
            Console.WriteLine("Reset Password Link: " + resetPasswordLink);
            bool isEmailSent = await _emailService.SendResetPasswordEmail(forgotPasswordViewModel.Email, resetPasswordLink);
            if (isEmailSent)
            {
                TempData["SuccessMessage"] = "A link has been sent to this email to reset your password.";
                return View(forgotPasswordViewModel);
            }
            else
            {
                TempData["ErrorMessage"] = "Some error occured while sending the link to this email. Please try again later.";
                return View(forgotPasswordViewModel);
            }
        }
        else
        {
            TempData["ErrorMessage"] = "Email is empty!";
            return View(forgotPasswordViewModel);
        }
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            TempData["ErrorMessage"] = "You do not have a reset password token!";
            return RedirectToAction("Login");
        }

        JsonResponse resetPasswordResult = await _resetPasswordService.ValidateResetPasswordToken(token);

        if (resetPasswordResult.Success)
        {
            TempData["Token"] = token;            
            string? email = resetPasswordResult.Anonymous;
            if (string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "Invalid reset password token!";
                return RedirectToAction("Login");
            }
            TempData["Email"] = email;
            return View();
        }
        else
        {
            TempData["ErrorMessage"] = resetPasswordResult.Message;
            return RedirectToAction("Login");
        }
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel resetPasswordViewModel)
    {
        if (ModelState.IsValid)
        {
            bool result = await _authService.ResetUserPassword(resetPasswordViewModel);
            if (result)
            {
                TempData["SuccessMessage"] = "Your password has been reset successfully! Please login to enter into the application";
                return RedirectToAction("Login");
            }
            TempData["ErrorMessage"] = "Some error occured in resetting your password!";
            return View(new { token = resetPasswordViewModel.Token });
        }
        else
        {
            TempData["ErrorMessage"] = "Some error occured in passing data to the server.";
            return View(resetPasswordViewModel);
        }
    }

    public IActionResult Logout()
    {
        if (Request.Cookies["JwtToken"] != null)
        {
            Response.Cookies.Delete("JwtToken");
        }

        if (Request.Cookies["ProfileImagePath"] != null)
        {
            Response.Cookies.Delete("ProfileImagePath");
        }

        return RedirectToAction("Login");
    }
}
