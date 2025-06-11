using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ElasticFind.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Nest;
using ElasticFind.Repository.ViewModels;
using ElasticFind.Repository.Interfaces;
using ElasticFind.Service.Interfaces;
using ElasticFind.Web.Connector;

namespace ElasticFind.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IUserService _userService;
    private readonly ConnectionToEs _connectionToEs;
    private readonly IElasticSearchService _elasticSearchService;
    private readonly IElasticClient _elasticClient;

    public HomeController(ILogger<HomeController> logger, IUserService userService, IElasticSearchService elasticSearchService, IElasticClient elasticClient)
    {
        _logger = logger;
        _userService = userService;
        _connectionToEs = new ConnectionToEs();
        _elasticSearchService = elasticSearchService;
        _elasticClient = elasticClient;
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

    public async Task<JsonResult> DeleteUser(int id)
    {
        bool result = await _userService.DeleteUser(id);
        if (result)
        {
            return Json(new { success = true, message = "User deleted successfully." });
        }
        else
        {
            return Json(new { success = false, message = "Error deleting user." });
        }
    }

    public async Task<JsonResult> ToggleUserStatus(int id)
    {
        bool result = await _userService.ToggleUserStatus(id);
        if (result)
        {
            return Json(new { success = true, message = "User status changed successfully!" });
        }
        else
        {
            return Json(new { success = false, message = "Error in changing status of the user." });
        }
    }

    [HttpGet]
    public ActionResult Search()
    {
        return View();
    }

    public async Task<List<Humanresources>> DataSearch(string keyword, string nationalIDNumber)
    {
        // var responsedata = _connectionToEs.EsClient().Search<Humanresources>(s => s
        //                         .Index("jobs")
        //                         .Query(q => q
        //                             .Match(m => m
        //                                 .Field(f => f.Jobtitle)
        //                                 .Query(keyword)
        //                             )
        //                         )
        //                     );

        // var datasend = (from hits in responsedata.Hits
        //                 select hits.Source).ToList();

        // return Json(new { datasend, responsedata.Took });

        var response = await _elasticSearchService.SearchByJobTitleAsync(keyword);
        return response;
    }

    [HttpPost]
    public async Task<IActionResult> UploadDocument(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file selected.");

        using var memoryStream = new MemoryStream();
        await file.CopyToAsync(memoryStream);
        var base64data = Convert.ToBase64String(memoryStream.ToArray());

        DocumentViewModel document = new()
        {
            Id = file.FileName + "-" + DateTime.Now.ToString("yyyyMMddHHmmssfff"),
            FileName = file.FileName,
            Data = base64data
        };

        var response = await _elasticClient.IndexAsync(document, i => i.Id(document.Id).Pipeline("attachment"));

        if (response.IsValid)
            return Ok("Document indexed successfully.");
        else
            return BadRequest(response.OriginalException.Message);
    }


    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
