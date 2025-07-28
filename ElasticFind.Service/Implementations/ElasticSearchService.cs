using System.Text;
using System.Text.Json;
using ElasticFind.Repository.ViewModels;
using ElasticFind.Service.Interfaces;
using Elasticsearch.Net;
using Microsoft.Extensions.Caching.Memory;
using Nest;
using Newtonsoft.Json.Linq;
using System;
using Microsoft.Extensions.Configuration;
using ElasticFind.Service.Exceptions;
using Serilog.Context;
using ElasticFind.Repository.Interfaces;

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
            indexName = indexName.ToLowerInvariant(); // Outputs lowercase index names irrespective of any culture (to maintain similarity of lowercase index names everywhere in our elasticsearch)

            var existsResponse = await _elasticClient.Indices.ExistsAsync(indexName);
            if (existsResponse.Exists)
                return true; // Already exists

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

    public async Task<bool> IndexAsync(Humanresources hr)
    {
        try
        {
            var response = await _elasticClient.IndexDocumentAsync(hr);
            return response.IsValid;
        }
        catch (Exception ex)
        {
            throw new ElasticSearchException("An unexpected error occurred while indexing the document.", ex);
        }
    }

    public async Task<List<Humanresources>> SearchByJobTitleAsync(string keyword)
    {
        try
        {
            var response = await _elasticClient.SearchAsync<Humanresources>(s => s
                .Query(q => q
                    .Match(m => m
                        .Field(f => f.Jobtitle)
                        .Query(keyword)
                    )
                )
            );

            return response.Documents.ToList();
        }
        catch (Exception ex)
        {
            throw new ElasticSearchException("An unexpected error occurred while searching the job title.", ex);
        }
    }

    public async Task<bool> UpdateAsync(Humanresources hr)
    {
        try
        {
            var response = await _elasticClient.IndexAsync(hr, i => i.Id(hr.Nationalidnumber));
            return response.IsValid;
        }
        catch (Exception ex)
        {
            throw new ElasticSearchException("An unexpected error occurred while updating the index.", ex);
        }
    }

    public async Task<bool> UpdateFieldAsync(int id, string newJobTitle)
    {
        try
        {
            var response = await _elasticClient.UpdateAsync<Humanresources>(id, u => u
                .Doc(new Humanresources { Jobtitle = newJobTitle })
            );
            return response.IsValid;
        }
        catch (Exception ex)
        {
            throw new ElasticSearchException("An unexpected error occurred while updating the field in the index.", ex);
        }
    }

    public async Task<bool> DeleteAsync(string id, string category)
    {
        try
        {
            var response = await _elasticClient.DeleteAsync<DocumentViewModel>(id, d => d.Index(category.ToLowerInvariant()).Refresh(Refresh.WaitFor));
            Console.WriteLine($"Delete response: {response.DebugInformation}");
            return response.IsValid;
        }
        catch (Exception ex)
        {
            throw new ElasticSearchException("An unexpected error occurred in deleting the file.", ex);
        }
    }

    public async Task<bool> DeleteMultipleFilesAsync(string id, string category)
    {
        try
        {
            var response = await _elasticClient.DeleteAsync<DocumentViewModel>(id, d => d.Index(category.ToLowerInvariant()));
            Console.WriteLine($"Delete response: {response.DebugInformation}");
            return response.IsValid;
        }
        catch (Exception ex)
        {
            throw new ElasticSearchException("An unexpected error occurred in deleting the files.", ex);
        }
    }

    public async Task<SearchResultsViewModel> SearchDocumentsAsync(string selectedCategory, string? sortBy = null, int currentPage = 1, int currentPageSize = 5, string? esBoolQuery = null)
    {
        try
        {
            if (!string.IsNullOrEmpty(esBoolQuery) && esBoolQuery != "{}")
            {
                var rawQuery = new RawQuery(esBoolQuery);

                // Detect fields used in query
                var usedFields = new HashSet<string>();
                using var doc = JsonDocument.Parse(esBoolQuery);
                CollectFields(doc.RootElement, usedFields);
                Console.WriteLine("Used Fields: " + string.Join(", ", usedFields));

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
                        Console.WriteLine("Highlighting fileName field");
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
                        Console.WriteLine("Highlighting fileType field");
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
                        Console.WriteLine("Highlighting uploadedDate field");
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

                // Sorting
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

                // Count
                var countResponse = await _elasticClient.CountAsync<DocumentViewModel>(c => c
                    .Index(selectedCategory.ToLowerInvariant())
                    .Query(q => rawQuery)
                );

                ISearchResponse<DocumentViewModel> response;

                // Check existence of index. If not, create one
                var indexExistsResponse = await _elasticClient.Indices.ExistsAsync(selectedCategory.ToLowerInvariant());
                if (!indexExistsResponse.Exists)
                {
                    Console.WriteLine($"Index '{selectedCategory.ToLowerInvariant()}' does not exist. Creating index.");
                    var createIndexResponse = await _elasticClient.Indices.CreateAsync(selectedCategory.ToLowerInvariant(), c => c
                        .Map<DocumentViewModel>(m => m.AutoMap())
                    );

                    if (!createIndexResponse.IsValid)
                    {
                        throw new ElasticSearchException($"Failed to create index '{selectedCategory.ToLowerInvariant()}'.");
                    }
                }

                if (currentPage == 0 && currentPageSize == 0)
                {
                    response = await _elasticClient.SearchAsync<DocumentViewModel>(s => s
                        .Index(selectedCategory.ToLowerInvariant())
                        .Query(q => rawQuery)
                        .Highlight(highlightBuilder)
                        .Sort(sort)
                        .Take((int)countResponse.Count)
                    );
                }
                else
                {
                    response = await _elasticClient.SearchAsync<DocumentViewModel>(s => s
                        .Index(selectedCategory.ToLowerInvariant())
                        .Query(q => rawQuery)
                        .Highlight(highlightBuilder)
                        .Sort(sort)
                        .Skip((currentPage - 1) * currentPageSize)
                        .Take(currentPageSize)
                    );
                }

                var decodedRequest = Encoding.UTF8.GetString(response.ApiCall.RequestBodyInBytes);
                Console.WriteLine("ElasticClient Request Decoded: " + decodedRequest);


                // Process results
                var results = new List<GroupedSearchResults>();
                foreach (var hit in response.Hits)
                {
                    var snippets = new List<string>();
                    var highlightedFileNames = new List<string>();
                    var highlightedFileTypes = new List<string>();
                    string? highlightedUploadedDate = null;

                    if (hit.Highlight.TryGetValue("attachment.content", out var contentHighlights))
                    {
                        Console.WriteLine("Content Highlights Found");
                        snippets.AddRange(contentHighlights);
                    }

                    if (hit.Highlight.TryGetValue("fileName", out var fileNameHighlights))
                    {
                        Console.WriteLine("File Name Highlights Found");
                        highlightedFileNames.AddRange(fileNameHighlights);
                    }

                    if (hit.Highlight.TryGetValue("fileType", out var fileTypeHighlights))
                    {
                        Console.WriteLine("File Type Highlights Found");
                        highlightedFileTypes.AddRange(fileTypeHighlights);
                    }

                    if (hit.Highlight.TryGetValue("uploadedDateText", out var uploadedDateHighlights))
                    {
                        Console.WriteLine("Uploaded Date Highlights Found");
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
                    // Add the first (and usually only) property as a field
                    foreach (var inner in prop.Value.EnumerateObject())
                        fields.Add(inner.Name);

                    // Recurse deeper
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
            // .Sort(st => string.IsNullOrEmpty(paginationViewModel.SortOrder) ? null : st.Field(f => f.FileName, paginationViewModel.SortOrder == "Asc" ? SortOrder.Ascending : SortOrder.Descending))
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
                Console.WriteLine($"Error fetching synonyms for '{word}': {response.StatusCode}");
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
}
