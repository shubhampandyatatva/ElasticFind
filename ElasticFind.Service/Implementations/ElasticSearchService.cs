using System.Text;
using System.Text.Json;
using ElasticFind.Repository.ViewModels;
using ElasticFind.Service.Interfaces;
using Elasticsearch.Net;
using Microsoft.Extensions.Caching.Memory;
using Nest;
using Microsoft.Extensions.Configuration;
using ElasticFind.Service.Exceptions;
using Serilog.Context;
using ElasticFind.Repository.Interfaces;
using Microsoft.AspNetCore.Http;
using ElasticFind.Repository.Data;
using Microsoft.AspNetCore.Mvc;

namespace ElasticFind.Service.Implementations;

public class ElasticSearchService : IElasticSearchService
{
    private readonly IElasticClient _elasticClient;
    private readonly IUserService _userService;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ICategoryRepository _categoryRepository;

    public ElasticSearchService(IElasticClient elasticClient, IUserService userService, IMemoryCache cache, IConfiguration configuration, ICategoryRepository categoryRepository)
    {
        _elasticClient = elasticClient;
        _userService = userService;
        _cache = cache;
        _configuration = configuration;
        _categoryRepository = categoryRepository;
    }

    public async Task<bool> CreateDocumentIndexAsync(string indexName)
    {
        try
        {
            indexName = indexName.ToLowerInvariant();

            var existsResponse = await _elasticClient.Indices.ExistsAsync(indexName);
            if (existsResponse.Exists)
                return true;

            var createIndexResponse = await _elasticClient.Indices.CreateAsync(indexName, c => c
                .Map<DocumentViewModel>(m => m
                    .AutoMap()
                )
            );

            return createIndexResponse.IsValid;
        }
        catch (Exception ex)
        {
            throw new ElasticSearchException("An unexpected error occurred while creating the index.", ex);
        }
    }

    public async Task<bool> DeleteAsync(string id, string category)
    {
        try
        {
            var response = await _elasticClient.DeleteAsync<DocumentViewModel>(id, d => d.Index(category.ToLowerInvariant()).Refresh(Refresh.WaitFor));
            return response.IsValid;
        }
        catch (Exception ex)
        {
            throw new ElasticSearchException("An unexpected error occurred in deleting the file.", ex);
        }
    }

    public async Task<JsonResult> DeleteMultipleFilesAsync(List<string> ids, string category)
    {
        var deleteMultipleFilesResponse = await _elasticClient.BulkAsync(b => b
            .Index(category.ToLowerInvariant())
            .DeleteMany<DocumentViewModel>(ids, (d, id) => d.Id(id))
        );

        if (deleteMultipleFilesResponse.Errors)
        {
            var errorMessages = deleteMultipleFilesResponse.ItemsWithErrors
                .Select(item => $"{item.Id}: {item.Error.Reason}")
                .ToList();

            throw new ElasticSearchException($"Failed to delete some documents: {string.Join(", ", errorMessages)}");
        }

        var refreshResponse = await _elasticClient.Indices.RefreshAsync(category.ToLowerInvariant());

        if (!refreshResponse.IsValid)
        {
            return new JsonResult(new { Success = true, warning = true, message = "Deletion completed successfully but there was some error refreshing the index!" });
        }

        return new JsonResult(new { Success = true, message = "Files deleted successfully!" });
    }

    public async Task<SearchResultsViewModel> SearchDocumentsAsync(string selectedCategory, string? sortBy = null, int currentPage = 1, int currentPageSize = 5, string? esBoolQuery = null)
    {
        try
        {
            if (!string.IsNullOrEmpty(esBoolQuery) && esBoolQuery != "{}")
            {
                var rawQuery = new RawQuery(esBoolQuery);

                var usedFields = new HashSet<string>();
                using var doc = JsonDocument.Parse(esBoolQuery);
                CollectFields(doc.RootElement, usedFields);

                // Prepare highlighting dynamically
                IHighlight highlightBuilder(HighlightDescriptor<DocumentViewModel> h)
                {
                    var highlightFields = new List<Func<HighlightFieldDescriptor<DocumentViewModel>, IHighlightField>>
                {
                    // Always include attachment.content
                    f => f
                        .Field("attachment.content")
                        .FragmentSize(200)
                        .NumberOfFragments(50)
                        .NoMatchSize(150)
                        .PreTags("<mark>")
                        .PostTags("</mark>")
                };

                    if (usedFields.Contains("fileName"))
                    {
                        highlightFields.Add(f => f
                            .Field("fileName")
                            .FragmentSize(200)
                            .NumberOfFragments(50)
                            .NoMatchSize(150)
                            .PreTags("<mark>")
                            .PostTags("</mark>")
                        );
                    }

                    if (usedFields.Contains("fileType"))
                    {
                        highlightFields.Add(f => f
                            .Field("fileType")
                            .FragmentSize(50)
                            .NumberOfFragments(1)
                            .NoMatchSize(20)
                            .PreTags("<mark>")
                            .PostTags("</mark>")
                        );
                    }

                    if (usedFields.Contains("uploadedDateText"))
                    {
                        highlightFields.Add(f => f
                            .Field("uploadedDateText")
                            .FragmentSize(50)
                            .NumberOfFragments(1)
                            .NoMatchSize(20)
                            .PreTags("<mark>")
                            .PostTags("</mark>")
                        );
                    }

                    return h.Fields(highlightFields.ToArray());
                }

                Func<SortDescriptor<DocumentViewModel>, IPromise<IList<ISort>>>? sort = null;
                if (!string.IsNullOrWhiteSpace(sortBy))
                {
                    sort = s =>
                    {
                        switch (sortBy)
                        {
                            case "1": s.Descending(f => f.UploadedDate); break;
                            case "2": s.Ascending(f => f.FileType.Suffix("keyword")); break;
                            case "3": s.Ascending(f => f.UploadedDate); break;
                        }
                        return s;
                    };
                }

                selectedCategory = selectedCategory.ToLowerInvariant();

                var indexExistsResponse = await _elasticClient.Indices.ExistsAsync(selectedCategory);
                if (!indexExistsResponse.Exists)
                {
                    var createIndexResponse = await _elasticClient.Indices.CreateAsync(selectedCategory, c => c
                        .Map<DocumentViewModel>(m => m.AutoMap())
                    );

                    if (!createIndexResponse.IsValid)
                    {
                        throw new ElasticSearchException($"Failed to create index '{selectedCategory}'.");
                    }
                }

                var countResponse = await _elasticClient.CountAsync<DocumentViewModel>(c => c
                    .Index(selectedCategory)
                    .Query(q => rawQuery)
                );

                ISearchResponse<DocumentViewModel> response;

                if (currentPage == 0 && currentPageSize == 0)
                {
                    response = await _elasticClient.SearchAsync<DocumentViewModel>(s => s
                        .Index(selectedCategory)
                        .Query(q => rawQuery)
                        .Highlight(highlightBuilder)
                        .Sort(sort)
                        .Take((int)countResponse.Count)
                    );
                }
                else
                {
                    response = await _elasticClient.SearchAsync<DocumentViewModel>(s => s
                        .Index(selectedCategory)
                        .Query(q => rawQuery)
                        .Highlight(highlightBuilder)
                        .Sort(sort)
                        .Skip((currentPage - 1) * currentPageSize)
                        .Take(currentPageSize)
                    );
                }

                var decodedRequest = Encoding.UTF8.GetString(response.ApiCall.RequestBodyInBytes);


                var results = new List<GroupedSearchResults>();
                foreach (var hit in response.Hits)
                {
                    var snippets = new List<string>();
                    var highlightedFileNames = new List<string>();
                    var highlightedFileTypes = new List<string>();
                    string? highlightedUploadedDate = null;

                    if (hit.Highlight.TryGetValue("attachment.content", out var contentHighlights))
                    {
                        snippets.AddRange(contentHighlights);
                    }

                    if (hit.Highlight.TryGetValue("fileName", out var fileNameHighlights))
                    {
                        highlightedFileNames.AddRange(fileNameHighlights);
                    }

                    if (hit.Highlight.TryGetValue("fileType", out var fileTypeHighlights))
                    {
                        highlightedFileTypes.AddRange(fileTypeHighlights);
                    }

                    if (hit.Highlight.TryGetValue("uploadedDateText", out var uploadedDateHighlights))
                    {
                        highlightedUploadedDate = uploadedDateHighlights.FirstOrDefault()?.Split('T')[0];
                    }

                    results.Add(new GroupedSearchResults
                    {
                        Id = hit.Id,
                        FileName = hit.Source.FileName,
                        UploadedDate = hit.Source.UploadedDate,
                        Snippets = snippets,
                        HighlightedFileName = highlightedFileNames.Count != 0 ? highlightedFileNames.FirstOrDefault() : hit.Source.FileName,
                        HighlightedFileType = highlightedFileTypes.Count != 0 ? highlightedFileTypes.FirstOrDefault() : hit.Source.FileType,
                        HighlightedUploadedDate = highlightedUploadedDate ?? hit.Source.UploadedDate?.ToString("yyyy-MM-dd"),
                    });
                }

                return new SearchResultsViewModel
                {
                    TotalDocuments = (int)countResponse.Count,
                    SearchResults = results
                };
            }

            return new SearchResultsViewModel();
        }
        catch (Exception ex)
        {
            throw new ElasticSearchException("An unexpected error occurred in searching the documents.", ex);
        }
    }

    static void CollectFields(JsonElement element, HashSet<string> fields)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Object)
                {
                    foreach (var inner in prop.Value.EnumerateObject())
                        fields.Add(inner.Name);

                    CollectFields(prop.Value, fields);
                }
                else if (prop.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in prop.Value.EnumerateArray())
                        CollectFields(item, fields);
                }
            }
        }
    }

    public async Task<DisplayFilesViewModel> GetFilesAsync(PaginationViewModel paginationViewModel, string indexName)
    {
        try
        {
            var searchResponse = await _elasticClient.SearchAsync<DocumentViewModel>(s => s
                .Index(indexName.ToLowerInvariant())
                .From((paginationViewModel.Page - 1) * paginationViewModel.PageSize)
                .Size(paginationViewModel.PageSize)
                .Query(q =>
                string.IsNullOrEmpty(paginationViewModel.SearchString)
                ? q.MatchAll()
                : q.Wildcard(w => w
                        .Field(f => f.FileName.Suffix("keyword"))
                        .Value($"*{paginationViewModel.SearchString.ToLowerInvariant()}*")
                    ))
                .Sort(st => st.Field(f => f.UploadedDate, SortOrder.Descending))
            );

            List<FileViewModel> files = new();
            foreach (var doc in searchResponse.Documents)
            {
                files.Add(new FileViewModel
                {
                    Id = doc.Id,
                    FileName = doc.FileName,
                    UploadedBy = await _userService.GetUserFullNameById(doc.UploadedBy),
                    UploadedDate = doc.UploadedDate
                });
            }

            paginationViewModel.TotalRecords = paginationViewModel.SearchString == null ?
                    (int)searchResponse.Total :
                    (int)(await _elasticClient.CountAsync<DocumentViewModel>(c => c
                    .Index(indexName.ToLowerInvariant())
                    .Query(q => q.Wildcard(w => w
                        .Field(f => f.FileName.Suffix("keyword"))
                        .Value($"*{paginationViewModel.SearchString.ToLowerInvariant()}*")
                    ))
                )).Count;

            List<CategoryViewModel> categories = await _categoryRepository.GetAllCategories();

            DisplayFilesViewModel displayFilesViewModel = new()
            {
                Pagination = paginationViewModel,
                Files = files,
                Categories = categories
            };

            return displayFilesViewModel;
        }
        catch (Exception ex)
        {
            throw new ElasticSearchException("An unexpected error occurred in fetching the files from the server.", ex);
        }
    }

    public async Task<List<string>?> GetSynonymsAsync(string word)
    {
        try
        {
            if (_cache.TryGetValue(word, out List<string>? cachedSynonyms))
                return cachedSynonyms;

            using var httpClient = new HttpClient();
            var url = $"https://api.datamuse.com/words?rel_syn={word}&max=10";

            var response = await httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                return new List<string>();
            }

            var content = await response.Content.ReadAsStringAsync();

            var json = JsonSerializer.Deserialize<List<DatamuseWord>>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var synonyms = json?
                .Select(w => w.Word)
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .Distinct()
                .Take(10)
                .ToList() ?? new List<string>();

            _cache.Set(word, synonyms, TimeSpan.FromHours(8)); // Cache for 8 hours

            return synonyms;
        }
        catch (Exception ex)
        {
            throw new ElasticSearchException("An unexpected error occurred while fetching synonyms.", ex);
        }
    }

    public async Task<List<string>> GetAllFileIdsAsync()
    {
        try
        {
            var searchResponse = await _elasticClient.SearchAsync<FileViewModel>(s => s
                .Index(_configuration["ElasticSearch:IndexName"])
                .Size(10000)  // Elasticsearch default limit is 10,000
                .Source(src => src.Includes(f => f.Field(fm => fm.Id)))
                .Query(q => q.MatchAll())
            );

            return searchResponse.Documents.Select(d => d.Id).ToList();
        }
        catch (Exception ex)
        {
            var stackTrace = new System.Diagnostics.StackTrace(ex, true);
            var lineNumber = stackTrace.GetFrame(0)?.GetFileLineNumber() ?? 0;
            using (LogContext.PushProperty("line_number", lineNumber))
            {
                throw new ElasticSearchException("An unexpected error occurred while fetching all file IDs from the server.", ex);
            }
        }
    }

    public async Task<List<string>> GetAllFileIdsByIndexAsync(string index)
    {
        try
        {
            var searchResponse = await _elasticClient.SearchAsync<FileViewModel>(s => s
                .Index(index.ToLowerInvariant())
                .Size(10000)  // Elasticsearch default limit is 10,000
                .Source(src => src.Includes(f => f.Field(fm => fm.Id)))
                .Query(q => q.MatchAll())
            );

            return searchResponse.Documents.Select(d => d.Id).ToList();
        }
        catch (Exception ex)
        {
            var stackTrace = new System.Diagnostics.StackTrace(ex, true);
            var lineNumber = stackTrace.GetFrame(0)?.GetFileLineNumber() ?? 0;
            using (LogContext.PushProperty("line_number", lineNumber))
            {
                throw new ElasticSearchException("An unexpected error occurred while fetching all file IDs from the server.", ex);
            }
        }
    }

    public async Task UploadDocumentsAsync(List<IFormFile> files, string uploadCategory, User user)
    {
        List<DocumentViewModel> documents = new();

        try
        {
            string fileNameWithoutExt;
            long timestamp = 0;
            string customId;
            string base64Data;
            foreach (var file in files)
            {
                if (file.Length == 0)
                    continue;

                fileNameWithoutExt = Path.GetFileNameWithoutExtension(file.FileName);
                timestamp = DateTime.UtcNow.Ticks;
                customId = $"{fileNameWithoutExt}_{timestamp}";

                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                base64Data = Convert.ToBase64String(ms.ToArray());

                var document = new DocumentViewModel
                {
                    Id = customId,
                    FileName = fileNameWithoutExt.ToLowerInvariant(),
                    FileType = Path.GetExtension(file.FileName).ToLowerInvariant(),
                    UploadedBy = user.Id.ToString(),
                    UploadedDate = DateTime.Now,
                    UploadedDateText = DateTime.Now.ToString("dd-MM-yyyy"),
                    Data = base64Data
                };

                documents.Add(document);
            }

            var bulkIndexResponse = await _elasticClient.BulkAsync(b => b
                    .Index(uploadCategory.ToLowerInvariant())
                    .Pipeline("attachment")
                    .IndexMany(documents)
            );

            if (bulkIndexResponse.Errors)
            {
                var errorMessages = bulkIndexResponse.ItemsWithErrors
                    .Select(item => $"{item.Id}: {item.Error.Reason}")
                    .ToList();

                throw new ElasticSearchException($"Failed to upload some documents!");
            }
        }
        catch (Exception e)
        {
            throw new ElasticSearchException("An unexpected error occurred while preparing the documents for upload.", e);
        }
    }
}