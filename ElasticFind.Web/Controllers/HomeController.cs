using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ElasticFind.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Nest;
using ElasticFind.Repository.ViewModels;
using ElasticFind.Repository.Interfaces;
using ElasticFind.Service.Interfaces;

namespace ElasticFind.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IUserService _userService;

    public HomeController(ILogger<HomeController> logger, IUserService userService)
    {
        _logger = logger;
        _userService = userService;
    }

    [Authorize(Roles = "1")]
    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> UploadFiles(List<IFormFile> files)
    {
        if (files == null || files.Count == 0)
        {
            TempData["UploadError"] = "Please select at least one file to upload.";
            return RedirectToAction("Index");
        }

        var uploadPath = Path.Combine("wwwroot/uploads");
        Directory.CreateDirectory(uploadPath);

        foreach (var file in files)
        {
            var originalFileName = Path.GetFileNameWithoutExtension(file.FileName);
            var extension = Path.GetExtension(file.FileName);

            // Remove special characters to ensure they do not interfere with OS or browsers
            var safeFileName = string.Concat(originalFileName.Split(Path.GetInvalidFileNameChars()));

            // Add timestamp
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");

            // Combine to create a unique filename
            var uniqueFileName = $"{safeFileName}-{timestamp}{extension}";

            var filePath = Path.Combine(uploadPath, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }
        }

        TempData["UploadSuccess"] = $"{files.Count} file(s) uploaded successfully.";
        return RedirectToAction("Index");
    }

    public async Task<IActionResult> Users(int page = 1, int pageSize = 5, string? searchString = null, string sortOrder = "Asc")
    {
        DisplayUsersViewModel listOfUsers = await _userService.GetUserList(page, pageSize, searchString, sortOrder);

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return PartialView("_UsersPartial", listOfUsers);
        }

        return View(listOfUsers);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
