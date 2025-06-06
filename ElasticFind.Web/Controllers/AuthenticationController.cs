using System.Threading.Tasks;
using ElasticFind.Repository.ViewModels;
using ElasticFind.Service.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ElasticFind.Web.Controllers;

public class AuthenticationController : Controller
{
    private readonly IAuthService _authService;
    private readonly IResetPasswordService _resetPasswordService;
    private readonly IEmailService _emailService;
    public AuthenticationController(IAuthService authService, IResetPasswordService resetPasswordService, IEmailService emailService)
    {
        _authService = authService;
        _resetPasswordService = resetPasswordService;
        _emailService = emailService;
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login([FromForm] LoginViewModel loginViewModel)
    {
        if (ModelState.IsValid)
        {
            JsonResponse response = await _authService.ValidateUser(loginViewModel.Email, loginViewModel.Password);
            if (response.Success)
            {
                TempData["SuccessMessage"] = response.Message;
                // return RedirectToAction("Index", "Home");
                return View(loginViewModel);
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
                TempData["SuccessMessage"] = response.Message;
                // return RedirectToAction("Index", "Home");
                return RedirectToAction("Register", "Authentication");
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
            return View(new {token = resetPasswordViewModel.Token});
        }
        else
        {
            TempData["ErrorMessage"] = "Some error occured in passing data to the server.";
            return View(resetPasswordViewModel);
        }
    }
}
