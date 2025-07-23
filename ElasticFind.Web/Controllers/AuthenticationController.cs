using System.Diagnostics;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using ElasticFind.Repository.Data;
using ElasticFind.Repository.ViewModels;
using ElasticFind.Service.Constants;
using ElasticFind.Service.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using Serilog.Context;

namespace ElasticFind.Web.Controllers;

public class AuthenticationController : Controller
{
    private readonly IAuthService _authService;
    private readonly IUserService _userService;
    private readonly IEmailService _emailService;
    public AuthenticationController(IAuthService authService, IUserService userService, IEmailService emailService)
    {
        _authService = authService;
        _userService = userService;
        _emailService = emailService;
    }

    [HttpGet]
    public async Task<IActionResult> Login()
    {
        if (!string.IsNullOrEmpty(StartupDiagnostics.ElasticsearchError))
        {
            TempData["ErrorMessage"] = StartupDiagnostics.ElasticsearchError;
            // Log.Error("Elasticsearch Error: " + StartupDiagnostics.ElasticsearchError);
            return View();
        }

        string? jwtToken = Request.Cookies["JwtToken"];
        if (jwtToken == null)
        {
            Log.Warning("No JWT Token found in cookies, redirecting to login page.");
            return View();
        }

        string? email = _userService.GetClaimValue(jwtToken, ClaimTypes.Email);
        if (email == null)
        {
            Log.Warning("No email claim found in JWT Token, redirecting to login page.");
            return View();
        }
        User? existingUser = await _authService.GetUserByEmail(email);
        if (existingUser == null)
        {
            Log.Warning("User with the email obtained from JWT token was not found, redirecting to login page.");
            return View();
        }

        string? roleName = _userService.GetClaimValue(jwtToken, ClaimTypes.Role);
        if (roleName == null)
        {
            Log.Warning("No role claim found in JWT Token, redirecting to login page.");
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
        Log.Information("User {UserName} accessed ElasticFind Home", existingUser.Username);
        return roleName == Roles.Admin ? RedirectToAction("Index", "Home") : RedirectToAction("Search", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> Login([FromForm] LoginViewModel loginViewModel)
    {
        if (!string.IsNullOrEmpty(StartupDiagnostics.ElasticsearchError))
        {
            Console.WriteLine("Elasticsearch Error: " + StartupDiagnostics.ElasticsearchError);
            TempData["ErrorMessage"] = StartupDiagnostics.ElasticsearchError;
            Log.Error("Elasticsearch Error: " + StartupDiagnostics.ElasticsearchError);
            return View();
        }

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
                    Log.Error("User by email in JWT token not found!");
                    return View(loginViewModel);
                }
                string token = _userService.GenerateJwtToken(user);

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
                Log.Information("User {UserName} logged in successfully", user.Username);

                return user.Role?.RoleName == Roles.Admin ? RedirectToAction("Index", "Home") : RedirectToAction("Search", "Home");
            }
            else
            {
                TempData["ErrorMessage"] = response.Message;
                Log.Warning("Login failed for user {Email}: {Message}", loginViewModel.Email, response.Message);
                return View(loginViewModel);
            }
        }
        else
        {
            TempData["ErrorMessage"] = "Some error occured!";
            Log.Error("Model state is invalid for login attempt with this email.");
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
                string token = _userService.GenerateJwtToken(user);

                CookieOptions cookieOptions = new()
                {
                    HttpOnly = true, // Prevent JavaScript access
                    Secure = true, // Ensure cookie is only sent over HTTPS
                    Expires = DateTime.UtcNow.AddHours(24)
                };
                Response.Cookies.Append("JwtToken", token, cookieOptions);

                TempData["SuccessMessage"] = response.Message;
                return RedirectToAction("Search", "Home");
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
            string resetPasswordToken = _userService.GenerateResetPasswordToken(forgotPasswordViewModel.Email);
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

        JsonResponse resetPasswordResult = await _userService.ValidateResetPasswordToken(token);

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
