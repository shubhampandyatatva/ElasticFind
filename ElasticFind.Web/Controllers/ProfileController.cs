using System.Security.Claims;
using ElasticFind.Repository.ViewModels;
using ElasticFind.Service.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ElasticFind.Web.Controllers;

public class ProfileController : Controller
{
    private readonly IProfileService _profileService;
    private readonly IJwtService _jwtService;
    private readonly IAddressService _addressService;
    public ProfileController(IProfileService profileService, IJwtService jwtService, IAddressService addressService)
    {
        _profileService = profileService;
        _jwtService = jwtService;
        _addressService = addressService;
    }

    [HttpGet]
    public IActionResult ChangePassword()
    {
        string? jwtToken = Request.Cookies["JwtToken"];
        if (string.IsNullOrEmpty(jwtToken))
        {
            TempData["Anonymous"] = "Please Login first to change your password.";
            return RedirectToAction("Authentication", "Login");
        }
        string? email = _jwtService.GetClaimValue(jwtToken, ClaimTypes.Email);
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
            JsonResponse response = await _profileService.ChangePassword(viewModel);
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
    public async Task<IActionResult> MyProfile()
    {
        string? jwtToken = Request.Cookies["JwtToken"];
        if (string.IsNullOrEmpty(jwtToken))
        {
            TempData["Anonymous"] = "Please Login first to change your password.";
            return RedirectToAction("Authentication", "Login");
        }
        string? email = _jwtService.GetClaimValue(jwtToken, ClaimTypes.Email);
        if (string.IsNullOrEmpty(email))
        {
            TempData["Anonymous"] = "Your authentication token is not valid! Please login again to continue.";
            return RedirectToAction("Authentication", "Login");
        }

        MyProfileViewModel myProfile = await _profileService.GetProfileByEmail(email);
        ViewBag.Id = myProfile.Id;
        ViewBag.Email = myProfile.Email;
        Console.WriteLine("Id in viewbag in profile service: " + @ViewBag.Id);
        ViewBag.FirstName = myProfile.FirstName;
        ViewBag.LastName = myProfile.LastName;
        return View(myProfile);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateProfile(MyProfileViewModel myProfileViewModel)
    {
        if (ModelState.IsValid)
        {
            JsonResponse response = await _profileService.UpdateProfile(myProfileViewModel);
            if (response.Success)
            {
                Console.WriteLine("Profile Image Path: " + response.Anonymous);
                //upload profile image path in cookies
                if (!string.IsNullOrEmpty(response.Anonymous))
                {
                    HttpContext.Response.Cookies.Append("ProfileImagePath", response.Anonymous, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true
                    });
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
            Console.WriteLine("Errorrrrrrr");
            foreach (var state in ModelState)
            {
                foreach (var error in state.Value.Errors)
                {
                    Console.WriteLine($"Model Error for {state.Key}: {error.ErrorMessage}");
                }
            }
            ViewBag.Id = myProfileViewModel.Id;
            TempData["ErrorMessage"] = "Model State is not valid!";
            return RedirectToAction("MyProfile", "Profile");
        }
    }
}
