using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ElasticFind.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Nest;
using ElasticFind.Repository.ViewModels;
using ElasticFind.Repository.Interfaces;
using ElasticFind.Service.Interfaces;
using ElasticFind.Web.Connector;
using System.Threading.Tasks;
using ElasticFind.Service.Implementations;

namespace ElasticFind.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IUserService _userService;
    private readonly ConnectionToEs _connectionToEs;
    private readonly IElasticSearchService _elasticSearchService;
    private readonly IElasticClient _elasticClient;
    private readonly IPreviewFileService _previewFileService;

    public HomeController(ILogger<HomeController> logger, IUserService userService, IElasticSearchService elasticSearchService, IElasticClient elasticClient, IPreviewFileService previewFileService)
    {
        _logger = logger;
        _userService = userService;
        _connectionToEs = new ConnectionToEs();
        _elasticSearchService = elasticSearchService;
        _elasticClient = elasticClient;
        _previewFileService = previewFileService;
    }

    [Authorize(Roles = "1")]
    public async Task<IActionResult> Index(int page = 1, int pageSize = 5, string? searchString = null, string? sortOrder = null)
    {
        PaginationViewModel pagination = new()
        {
            Page = page,
            PageSize = pageSize,
            SearchString = searchString,
            SortOrder = sortOrder
        };

        DisplayFilesViewModel displayFilesViewModel = await _elasticSearchService.GetFilesAsync(pagination);

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return PartialView("_FilesPartial", displayFilesViewModel);
        }

        return View(displayFilesViewModel);
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
        var response = await _elasticSearchService.SearchByJobTitleAsync(keyword);
        return response;
    }

    [HttpPost]
    public async Task<IActionResult> UploadDocument(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file selected.");

        // Generate ID: FileName (without extension) + Timestamp
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file.FileName);
        var timestamp = DateTime.UtcNow.Ticks;
        var customId = $"{fileNameWithoutExt}_{timestamp}";

        // Read and convert file to base64
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var fileBytes = ms.ToArray();
        var base64Data = Convert.ToBase64String(fileBytes);

        DocumentViewModel document = new()
        {
            Id = customId,
            FileName = file.FileName,
            Data = base64Data
        };

        var response = await _elasticClient.IndexAsync(document, i => i.Id(document.Id).Index("documents").Pipeline("attachment"));

        if (response.IsValid)
            return Ok("Document indexed successfully.");
        else
            return BadRequest(response.OriginalException.Message);
    }

    [HttpPost]
    public async Task<IActionResult> CreateDocumentIndex(string indexName = "documents")
    {
        var created = await _elasticSearchService.CreateDocumentIndexAsync(indexName);

        if (created)
            return Ok("Index created");
        else
            return StatusCode(500, "Failed to create index");
    }

    [HttpPost]
    public async Task<IActionResult> SearchDocumentContent(string keyword)
    {
        var results = await _elasticSearchService.SearchDocumentsAsync(keyword);
        return Json(results);
    }

    [HttpGet]
    public async Task<IActionResult> Download(string id)
    {
        var response = await _elasticClient.GetAsync<DocumentViewModel>(id, x => x.Index("documents"));

        if (!response.Found)
            return NotFound("Document not found.");

        var fileBytes = Convert.FromBase64String(response.Source.Data);

        // Infer content type from extension
        var ext = Path.GetExtension(response.Source.FileName).ToLower();
        var contentType = ext switch
        {
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".doc" => "application/msword",
            _ => "application/octet-stream"
        };

        return File(fileBytes, contentType, response.Source.FileName);
    }

    [HttpGet]
    public async Task<IActionResult> Preview(string id)
    {
        var response = await _elasticClient.GetAsync<DocumentViewModel>(id, x => x.Index("documents"));
        if (!response.Found) return NotFound();

        var fileName = response.Source.FileName;
        var fileBytes = Convert.FromBase64String(response.Source.Data);

        var html = _previewFileService.GetPreviewHtml(fileName, fileBytes);
        if (!string.IsNullOrEmpty(html))
        {
            Console.WriteLine("HTML is not null!");
            return Content(html, "text/html");  
        }

        // Fallback for direct rendering (PDF, TXT, etc.)
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var contentType = ext switch
        {
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".html" => "text/html",
            _ => "application/octet-stream"
        };

        Response.Headers["Content-Disposition"] = $"inline; filename=\"{fileName}\"";
        return File(fileBytes, contentType);
    }

    public async Task<IActionResult> DeleteFile(string id)
    {
        bool result = await _elasticSearchService.DeleteAsync(id);

        if (result)
        {
            TempData["DeleteSuccess"] = "Document deleted successfully.";
            return RedirectToAction("Index");
        }
        else
        {
            TempData["DeleteError"] = "Error deleting document!";
            return RedirectToAction("Index");
        }
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
