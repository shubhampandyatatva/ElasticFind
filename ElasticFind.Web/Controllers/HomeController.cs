using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ElasticFind.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Nest;
using ElasticFind.Repository.ViewModels;
using ElasticFind.Repository.Interfaces;
using ElasticFind.Service.Interfaces;
using Rotativa.AspNetCore;
using System.Security.Claims;
using System.Text;
using Jose;
using Newtonsoft.Json;
using ElasticFind.Service.Constants;
using ElasticFind.Service.Exceptions;
using Serilog;

namespace ElasticFind.Web.Controllers;

[Authorize(Roles = Roles.Admin)]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IUserService _userService;
    private readonly IElasticSearchService _elasticSearchService;
    private readonly IElasticClient _elasticClient;
    private readonly IPreviewFileService _previewFileService;
    private readonly IExportService _exportService;
    private readonly IConfiguration _config;
    private readonly ICategoryRepository _categoryRepository;

    public HomeController(ILogger<HomeController> logger, IUserService userService, IElasticSearchService elasticSearchService, IElasticClient elasticClient, IPreviewFileService previewFileService, IExportService exportService, IConfiguration config, ICategoryRepository categoryRepository)
    {
        _logger = logger;
        _userService = userService;
        _elasticSearchService = elasticSearchService;
        _elasticClient = elasticClient;
        _previewFileService = previewFileService;
        _exportService = exportService;
        _config = config;
        _categoryRepository = categoryRepository;
    }

    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Index(string selectedCategory, int page = 1, int pageSize = 5, string? searchString = null, string? sortOrder = null)
    {
        string index = selectedCategory ?? await _categoryRepository.GetFirstCategory();
        PaginationViewModel pagination = new()
        {
            Page = page,
            PageSize = pageSize,
            SearchString = searchString,
            SortOrder = sortOrder
        };

        DisplayFilesViewModel displayFilesViewModel = await _elasticSearchService.GetFilesAsync(pagination, index.ToLowerInvariant());

        var allFileIds = await _elasticSearchService.GetAllFileIdsByIndexAsync(index.ToLowerInvariant());
        ViewBag.AllFileIds = allFileIds;

        ViewBag.SelectedCategory = index;

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

    [HttpPost]
    [RequestSizeLimit(100_000_000)] // 100 MB
    [RequestFormLimits(MultipartBodyLengthLimit = 100_000_000)]
    public async Task<IActionResult> UploadDocuments(List<IFormFile> files, string uploadCategory)
    {
        try
        {
            if (files == null || !files.Any())
                return new JsonResult(new { Success = false, message = "No files were received!" });

            if (string.IsNullOrEmpty(uploadCategory))
                return new JsonResult(new { Success = false, message = "No category was received!" });

            string? jwtToken = Request.Cookies["jwtToken"];
            if (jwtToken == null)
            {
                return new JsonResult(new { Success = false, message = "Unauthorized: JWT token is missing." });
            }
            string? email = _userService.GetClaimValue(jwtToken, ClaimTypes.Email);
            if (email == null)
            {
                return new JsonResult(new { Success = false, message = "Unauthorized: Email cannot be retrieved from JWT token." });
            }
            var user = await _userService.GetUserByEmail(email);
            if (user == null)
            {
                return new JsonResult(new { Success = false, message = "Error: User not found by this email." });
            }

            await _elasticSearchService.UploadDocumentsAsync(files, uploadCategory.ToLowerInvariant(), user);

            var refreshResponse = await _elasticClient.Indices.RefreshAsync(uploadCategory.ToLowerInvariant());

            if (!refreshResponse.IsValid)
            {
                return new JsonResult(new { Success = true, warning = true, message = "Deletion completed successfully but there was some error refreshing the index!" });
            }

            return new JsonResult(new { Success = true, message = "Files uploaded successfully." });
        }
        catch (Exception e)
        {
            Log.Error(e, "Error uploading documents");
            return StatusCode(500, "An error occurred while uploading the documents. Please try again later.");
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateDocumentIndex(string indexName = "documents")
    {
        var created = await _elasticSearchService.CreateDocumentIndexAsync(indexName);

        if (created)
        {
            return Ok("Index created");
        }
        else
        {
            return StatusCode(500, "Failed to create index");
        }
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> SearchDocumentContent(string selectedCategory, string? sortBy = null, int currentPage = 1, int currentPageSize = 5, string? esBoolQuery = null)
    {
        SearchResultsViewModel results = await _elasticSearchService.SearchDocumentsAsync(selectedCategory, sortBy, currentPage, currentPageSize, esBoolQuery);
        return Json(results);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Download(string fileId, string category)
    {
        var response = await _elasticClient.GetAsync<DocumentViewModel>(fileId, x => x.Index(category.ToLowerInvariant()));

        if (!response.Found)
            return NotFound("Document not found.");

        var fileBytes = Convert.FromBase64String(response.Source.Data);

        // Infer content type from extension
        var ext = response.Source.FileType.ToLower();
        var contentType = ext switch
        {
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".doc" => "application/msword",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".xls" => "application/vnd.ms-excel",
            "txt" => "text/plain",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".rtf" => "application/rtf",
            ".csv" => "text/csv",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".html" => "text/html",
            ".png" => "image/png",
            ".dotx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.template",
            ".dot" => "application/msword",
            ".odt" => "application/vnd.oasis.opendocument.text",
            ".ods" => "application/vnd.oasis.opendocument.spreadsheet",
            _ => "application/octet-stream"
        };

        return File(fileBytes, contentType, response.Source.FileName + ext);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Preview(string id)
    {
        var response = await _elasticClient.GetAsync<DocumentViewModel>(id, x => x.Index("documents"));
        if (!response.Found) return NotFound();

        var fileName = response.Source.FileName;
        var fileBytes = Convert.FromBase64String(response.Source.Data);

        var html = _previewFileService.GetPreviewHtml(fileName, fileBytes);
        if (!string.IsNullOrEmpty(html))
        {
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

    public async Task<IActionResult> DeleteFile(string id, string category)
    {
        bool result = await _elasticSearchService.DeleteAsync(id, category);

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
        var fileUrl = $"http://127.0.0.1:5052/Home/DownloadFileForViewer?id={Uri.EscapeDataString(file.Id)}";
        var ext = Path.GetExtension(file.FileName).Trim('.').ToLower();

        string? secret = _config["OnlyOffice:JwtSecret"];

        var documentConfig = new
        {
            document = new
            {
                title = file.FileName,
                url = $"http://localhost:5052/Home/DownloadFileForViewer?id={Uri.EscapeDataString(file.Id)}",
                fileType = ext,
                key = file.Id,
                directUrl = $"http://localhost:5052/Home/DownloadFileForViewer?id={Uri.EscapeDataString(file.Id)}",
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
                parentOrigin = "http://127.0.0.1:5052"
            },
            documentType = ext,
            width = "100%",
            height = "100%",
            type = "desktop",
            documentServerUrl = "http://127.0.0.1:5052"
        };

        // Sign with JWT if configured
        if (!string.IsNullOrEmpty(secret))
        {
            var payload = JsonConvert.SerializeObject(documentConfig);
            var token = JWT.Encode(payload, Encoding.UTF8.GetBytes(secret), JwsAlgorithm.HS256);

            var documentConfigWithToken = new
            {
                documentConfig.document,
                documentConfig.editorConfig,
                documentConfig.documentType,
                documentConfig.width,
                documentConfig.height,
                documentConfig.type,
                token,
                documentConfig.documentServerUrl
            };
            return Json(documentConfigWithToken);
        }

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
        var docConfig = new
        {
            document = new
            {
                fileType = "docx",
                title = "Test Document",
                url = $"http://localhost:5052/docs/test.docx",
                directUrl = $"http://localhost:5052/docs/test.docx",
                key = id
            },
            documentType = "word",
            editorConfig = new
            {
                mode = "view",
                lang = "en",
                customization = new
                {
                    forcesave = false
                }
            }
        };

        var token = _userService.GenerateJwtTokenForOnlyOffice(docConfig);

        ViewBag.ConfigJson = JsonConvert.SerializeObject(docConfig);
        ViewBag.Token = token;

        return View();
    }

    [HttpPost]
    [Authorize]
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
        return File(fileBytes, mimeType, file.FileName);
    }

    [Authorize]
    public async Task<IActionResult> ExportResultsToExcel(string selectedCategory, string? sortBy = null, int currentPage = 1, int pageSize = 5, string? esBoolQuery = null)
    {
        SearchResultsViewModel results = await _elasticSearchService.SearchDocumentsAsync(selectedCategory, sortBy, currentPage, pageSize, esBoolQuery);
        if (results == null)
        {
            return NotFound("No results found for the given criteria.");
        }
        return Ok("Export to Excel is not implemented yet.");
    }

    public async Task<JsonResult> DeleteMultipleFiles(List<string> ids, string category)
    {
        if (ids == null || !ids.Any())
        {
            return Json(new { success = false, message = "No files selected for deletion." });
        }

        return await _elasticSearchService.DeleteMultipleFilesAsync(ids, category);
    }

    [HttpGet]
    [Authorize]
    public IActionResult Search()
    {
        return View();
    }

    public async Task<IActionResult> GetAllCategories()
    {
        List<CategoryViewModel> categories = await _categoryRepository.GetAllCategories();
        return Json(categories);
    }

    public async Task<IActionResult> DownloadAllDocuments(string selectedCategory, string? sortBy = null, string? esBoolQuery = null)
    {
        ZipDownloadResult zipModel = await _exportService.ExportAllDocumentsToZip(selectedCategory, sortBy, esBoolQuery);
        return File(zipModel.FileBytes, zipModel.ContentType, zipModel.FileName);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
