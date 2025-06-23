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
using Elastic.Clients.Elasticsearch;
using Rotativa.AspNetCore;
using System.Security.Claims;
using ElasticFind.Repository.Data;

namespace ElasticFind.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IUserService _userService;
    private readonly ConnectionToEs _connectionToEs;
    private readonly IElasticSearchService _elasticSearchService;
    private readonly IElasticClient _elasticClient;
    private readonly IPreviewFileService _previewFileService;
    private readonly IExportService _exportService;
    private readonly IJwtService _jwtService;

    public HomeController(ILogger<HomeController> logger, IUserService userService, IElasticSearchService elasticSearchService, IElasticClient elasticClient, IPreviewFileService previewFileService, IExportService exportService, IJwtService jwtService)
    {
        _logger = logger;
        _userService = userService;
        _connectionToEs = new ConnectionToEs();
        _elasticSearchService = elasticSearchService;
        _elasticClient = elasticClient;
        _previewFileService = previewFileService;
        _exportService = exportService;
        _jwtService = jwtService;
    }

    [Authorize(Roles = "1")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
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

        // bool result = await _elasticSearchService.CreateDocumentIndexAsync("documents");
        // Console.WriteLine("Index creation result: " + result);

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

    [Authorize(Roles = "1")]
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
    [Authorize]
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
    public async Task<IActionResult> UploadDocuments(List<IFormFile> files)
    {
        if (files == null || !files.Any())
            return BadRequest("No files selected.");

        string? jwtToken = Request.Cookies["jwtToken"];
        if (jwtToken == null)
        {
            return BadRequest("Unauthorized: JWT token is missing.");
        }
        string? email = _jwtService.GetClaimValue(jwtToken, ClaimTypes.Email);
        if (email == null)
        {
            return BadRequest("Unauthorized: Email cannot be retrieved from JWT token.");
        }
        var user = await _userService.GetUserByEmail(email);
        if (user == null)
        {
            return BadRequest("Error: User not found by this email.");
        }

        foreach (var file in files)
        {
            try
            {
                if (file.Length == 0)
                    return BadRequest("One or more files are empty.");

                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(file.FileName);
                var timestamp = DateTime.UtcNow.Ticks;
                var customId = $"{fileNameWithoutExt}_{timestamp}";

                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                var fileBytes = ms.ToArray();
                var base64Data = Convert.ToBase64String(fileBytes);

                var document = new DocumentViewModel
                {
                    Id = customId,
                    FileName = file.FileName.ToLowerInvariant(),
                    FileType = Path.GetExtension(file.FileName).ToLowerInvariant(),
                    // UploadedBy = User.Identity?.Name ?? "Anonymous",
                    UploadedBy = $"{user.FirstName} {user.LastName}",
                    UploadedDate = DateTime.UtcNow,
                    Data = base64Data
                };

                var response = await _elasticClient.IndexAsync(document, i => i
                    .Id(document.Id)
                    .Index("documents")
                    .Pipeline("attachment")
                    .Refresh(Elasticsearch.Net.Refresh.WaitFor));

                if (!response.IsValid)
                    return BadRequest("Some error occurred in uploading the files.");
            }
            catch
            {
                return BadRequest("Some error occurred in uploading the files.");
            }
        }

        return Ok("Files uploaded successfully.");
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
    public async Task<IActionResult> SearchDocumentContent(string keyword, string? fileTypeFilter = null, DateTime? startDate = null, DateTime? endDate = null, string? sortBy = null, string? searchInput = null)
    {
        List<GroupedSearchResults> results = await _elasticSearchService.SearchDocumentsAsync(keyword, fileTypeFilter, startDate, endDate, sortBy, searchInput);
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
            return Json(new { success = true, message = "File deleted successfully." });
        }
        else
        {
            return Json(new { success = false, message = "There was an error deleting the file." });
        }
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> OnlyOfficeViewer(string id)
    {
        var response = await _elasticClient.GetAsync<DocumentViewModel>(id, g => g.Index("documents"));
        if (!response.Found)
            return NotFound();

        var file = response.Source;
        var fileUrl = $"http://192.168.4.90:5052/Home/DownloadFileForViewer?id={Uri.EscapeDataString(file.Id)}";
        var ext = Path.GetExtension(file.FileName).Trim('.').ToLower();
        Console.WriteLine("File Extension: " + ext);

        var documentConfig = new
        {
            document = new
            {
                title = file.FileName,
                url = $"http://127.0.0.1:5052/Home/DownloadFileForViewer?id={Uri.EscapeDataString(file.Id)}",
                // url = $"http://localhost:5052/Home/DownloadFileForViewer?id={Uri.EscapeDataString(file.Id)}",
                fileType = ext,
                key = file.Id,
                directUrl = $"http://127.0.0.1:5052/Home/DownloadFileForViewer?id={Uri.EscapeDataString(file.Id)}",
                permissions = new
                {
                    download = true,
                    print = true,
                    edit = false
                }
            },
            editorConfig = new
            {
                mode = "view",
                lang = "en",
                parentOrigin = "http://192.168.4.90:5052"
            },
            documentType = ext,
            width = "100%",
            height = "100%",
            type = "desktop",
            documentServerUrl = "http://192.168.4.90/"
        };

        return Json(documentConfig);
    }

    // [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadFileForViewer(string id)
    {
        var response = await _elasticClient.GetAsync<DocumentViewModel>(id, g => g.Index("documents"));
        if (!response.Found)
            return NotFound();

        var file = response.Source;
        var fileBytes = Convert.FromBase64String(file.Data);
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var mimeType = ext switch
        {
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".doc" => "application/msword",
            ".html" => "text/html",
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".csv" => "text/csv",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".xls" => "application/vnd.ms-excel",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".rtf" => "application/rtf",
            _ => "application/octet-stream"
        };

        return File(fileBytes, mimeType, file.FileName);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult OnlyOfficeViewerPage(string id)
    {
        ViewBag.FileId = id;
        return View();
    }

    [HttpPost]
    public IActionResult ExportToPdf([FromBody] ExportResultViewModel model)
    {
        // Return Razor view as PDF using Rotativa
        return new ViewAsPdf("ExportResults", model)
        {
            FileName = "ElasticFind_Results.pdf",
            PageSize = Rotativa.AspNetCore.Options.Size.A4,
            PageMargins = new Rotativa.AspNetCore.Options.Margins(20, 10, 20, 10)
        };
    }

    [HttpGet("/downloadfile/{id}")]
    [HttpPost("/downloadfile/{id}")]
    public async Task<IActionResult> DownloadFileForOnlyOffice(string id)
    {
        var response = await _elasticClient.GetAsync<DocumentViewModel>(id, g => g.Index("documents"));
        if (!response.Found)
            return NotFound();

        var file = response.Source;
        var fileBytes = Convert.FromBase64String(file.Data);
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        Console.WriteLine("Heyyyyyyyyyyyyy");
        var mimeType = ext switch
        {
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".doc" => "application/msword",
            ".html" => "text/html",
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".csv" => "text/csv",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".xls" => "application/vnd.ms-excel",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".rtf" => "application/rtf",
            _ => "application/octet-stream"
        };

        Response.Headers["Accept-Ranges"] = "bytes"; // important for partial PDF loads
        // Response.Headers["Content-Disposition"] = $"attachment; filename=\"{file.FileName}\"";
        // Response.Headers["Access-Control-Allow-Origin"] = "*";
        Console.WriteLine("Request Method: " + Request.Method);
        return File(fileBytes, mimeType, file.FileName);
    }

    public async Task<IActionResult> ExportResultsToExcel(string keyword, string fileType, DateTime? startDate, DateTime? endDate, string sortBy, string searchString)
    {
        List<GroupedSearchResults> results = await _elasticSearchService.SearchDocumentsAsync(keyword, fileType, startDate, endDate, sortBy, searchString);
        if (results == null || !results.Any())
        {
            return NotFound("No results found for the given criteria.");
        }
        byte[] fileBytes = _exportService.ExportSearchResultsToExcel(results, keyword, fileType, startDate, endDate, sortBy, searchString, results.Count);

        return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "ElasticFind_Results.xlsx");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
