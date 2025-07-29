using System.Security.Claims;
using ElasticFind.Repository.ViewModels;
using ElasticFind.Service.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElasticFind.Web.Controllers;

public class ProfileController : Controller
{
    private readonly IUserService _userService;
    public ProfileController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    [Authorize]
    public IActionResult ChangePassword() 
    {
        string? jwtToken = Request.Cookies["JwtToken"];
        if (string.IsNullOrEmpty(jwtToken))
        {
            TempData["Anonymous"] = "Please Login first to change your password.";
            return RedirectToAction("Authentication", "Login");
        }
        string? email = _userService.GetClaimValue(jwtToken, ClaimTypes.Email);
        if (string.IsNullOrEmpty(email))
        {
            TempData["Anonymous"] = "Your authentication token is not valid! Please login again to continue.";
            return RedirectToAction("Authentication", "Login");
        }
        TempData["Email"] = email;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel viewModel)
    {
        if (ModelState.IsValid)
        {
            JsonResponse response = await _userService.ChangePassword(viewModel);
            if (response.Success)
            {
                TempData["Email"] = viewModel.Email;
                TempData["SuccessMessage"] = "Your Password has been updated successfully!";
                return View(viewModel);
            }
            else
            {
                TempData["Email"] = viewModel.Email;
                TempData["ErrorMessage"] = response.Message;
                return View(viewModel);
            }
        }
        else
        {
            TempData["Email"] = viewModel.Email;
            TempData["ErrorMessage"] = "Model State is not valid!";
            return View(viewModel);
        }
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> MyProfile()
    {
        string? jwtToken = Request.Cookies["JwtToken"];
        if (string.IsNullOrEmpty(jwtToken))
        {
            TempData["Anonymous"] = "Please Login first to change your password.";
            return RedirectToAction("Authentication", "Login");
        }
        string? email = _userService.GetClaimValue(jwtToken, ClaimTypes.Email);
        if (string.IsNullOrEmpty(email))
        {
            TempData["Anonymous"] = "Your authentication token is not valid! Please login again to continue.";
            return RedirectToAction("Authentication", "Login");
        }

        MyProfileViewModel? myProfile = await _userService.GetProfileByEmail(email);
        if (myProfile == null)
        {
            TempData["ErrorMessage"] = "Profile not found!";
            return RedirectToAction("Index", "Home");
        }
        ViewBag.Id = myProfile.Id;
        ViewBag.Email = myProfile.Email;
        ViewBag.FirstName = myProfile.FirstName;
        ViewBag.LastName = myProfile.LastName;
        return View(myProfile);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateProfile(MyProfileViewModel myProfileViewModel)
    {
        if (ModelState.IsValid)
        {
            JsonResponse response = await _userService.UpdateProfile(myProfileViewModel);
            if (response.Success)
            {
                //upload profile image path in cookies
                if (!string.IsNullOrEmpty(response.Anonymous))
                {
                    HttpContext.Response.Cookies.Append("ProfileImagePath", response.Anonymous, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true
                    });
                } else
                {
                    HttpContext.Response.Cookies.Delete("ProfileImagePath");
                }
                ViewBag.Id = myProfileViewModel.Id;
                TempData["SuccessMessage"] = "Your profile has been updated successfully!";
                return RedirectToAction("MyProfile", "Profile");
            }
            else
            {
                ViewBag.Id = myProfileViewModel.Id;
                TempData["ErrorMessage"] = response.Message;
                return RedirectToAction("MyProfile", "Profile");
            }
        }
        else
        {
            ViewBag.Id = myProfileViewModel.Id;
            TempData["ErrorMessage"] = "Model State is not valid!";
            return RedirectToAction("MyProfile", "Profile");
        }
    }
}
