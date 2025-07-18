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
    private readonly IResetPasswordService _resetPasswordService;
    private readonly IEmailService _emailService;
    private readonly IJwtService _jwtService;
    private readonly Serilog.ILogger _logger;
    public AuthenticationController(IAuthService authService, IResetPasswordService resetPasswordService, IEmailService emailService, IJwtService jwtService, Serilog.ILogger logger)
    {
        _authService = authService;
        _resetPasswordService = resetPasswordService;
        _emailService = emailService;
        _jwtService = jwtService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Login()
    {
        if (!string.IsNullOrEmpty(StartupDiagnostics.ElasticsearchError))
        {
            Console.WriteLine("Elasticsearch Error: " + StartupDiagnostics.ElasticsearchError);
            TempData["ErrorMessage"] = StartupDiagnostics.ElasticsearchError;
            return View();
        }

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

        string? roleName = _jwtService.GetClaimValue(jwtToken, ClaimTypes.Role);
        if (roleName == null)
        {
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
        return roleName == Roles.Admin ? RedirectToAction("Index", "Home") : RedirectToAction("Search", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> Login([FromForm] LoginViewModel loginViewModel)
    {
        if (!string.IsNullOrEmpty(StartupDiagnostics.ElasticsearchError))
        {
            Console.WriteLine("Elasticsearch Error: " + StartupDiagnostics.ElasticsearchError);
            TempData["ErrorMessage"] = StartupDiagnostics.ElasticsearchError;
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

                using (LogContext.PushProperty("EnvironmentName", Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"))
                using (LogContext.PushProperty("Exception", null)) // no exception
                using (LogContext.PushProperty("FilePath", HttpContext.Request.Path))
                using (LogContext.PushProperty("IPAddress", HttpContext.Connection.RemoteIpAddress?.ToString()))
                using (LogContext.PushProperty("Level", "Information"))
                using (LogContext.PushProperty("LineNumber", new StackTrace(true).GetFrame(0)?.GetFileLineNumber()))
                using (LogContext.PushProperty("MachineName", Environment.MachineName))
                using (LogContext.PushProperty("Message", "User login successful"))
                using (LogContext.PushProperty("MessageTemplate", "User {UserName} logged in successfully"))
                using (LogContext.PushProperty("MethodName", MethodBase.GetCurrentMethod()?.Name))
                using (LogContext.PushProperty("ProcessInfo", $"PID: {Environment.ProcessId}, App: {Assembly.GetEntryAssembly()?.GetName().Name}"))
                using (LogContext.PushProperty("Properties", null)) // optional custom props
                using (LogContext.PushProperty("PropsTest", "login-test")) // sample static value
                using (LogContext.PushProperty("RaiseDate", DateTime.Now))
                using (LogContext.PushProperty("ThreadId", Environment.CurrentManagedThreadId))
                using (LogContext.PushProperty("UserAgent", Request.Headers["User-Agent"].ToString()))
                using (LogContext.PushProperty("UserName", user.Username))
                {
                    _logger.Information("User {UserName} logged in successfully", user.Username);
                    Log.Information("User {UserName} logged in successfully", user.Username);
                    Console.WriteLine($"User {user.Username} logged in successfully");
                }

                return user.Role?.RoleName == Roles.Admin ? RedirectToAction("Index", "Home") : RedirectToAction("Search", "Home");
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
