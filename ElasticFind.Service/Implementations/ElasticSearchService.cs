using System.Text;
using System.Text.Json;
using ElasticFind.Repository.ViewModels;
using ElasticFind.Service.Interfaces;
using Elasticsearch.Net;
using Microsoft.Extensions.Caching.Memory;
using Nest;
using Newtonsoft.Json.Linq;

namespace ElasticFind.Service.Implementations;

public class ElasticSearchService : IElasticSearchService
{
    private readonly IElasticClient _elasticClient;
    private readonly IUserService _userService;
    private readonly IMemoryCache _cache;

    public ElasticSearchService(IElasticClient elasticClient, IUserService userService, IMemoryCache cache)
    {
        _elasticClient = elasticClient;
        _userService = userService;
        _cache = cache;
    }

    public async Task<bool> CreateDocumentIndexAsync(string indexName)
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

    public async Task<bool> IndexAsync(Humanresources hr)
    {
        var response = await _elasticClient.IndexDocumentAsync(hr);
        return response.IsValid;
    }

    public async Task<List<Humanresources>> SearchByJobTitleAsync(string keyword)
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

    public async Task<bool> UpdateAsync(Humanresources hr)
    {
        var response = await _elasticClient.IndexAsync(hr, i => i.Id(hr.Nationalidnumber));
        return response.IsValid;
    }

    public async Task<bool> UpdateFieldAsync(int id, string newJobTitle)
    {
        var response = await _elasticClient.UpdateAsync<Humanresources>(id, u => u
            .Doc(new Humanresources { Jobtitle = newJobTitle })
        );
        return response.IsValid;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var response = await _elasticClient.DeleteAsync<DocumentViewModel>(id, d => d.Index("documents").Refresh(Refresh.WaitFor));
        Console.WriteLine($"Delete response: {response.DebugInformation}");
        return response.IsValid;
    }

    public async Task<bool> DeleteMultipleFilesAsync(string id)
    {
        var response = await _elasticClient.DeleteAsync<DocumentViewModel>(id, d => d.Index("documents"));
        Console.WriteLine($"Delete response: {response.DebugInformation}");
        return response.IsValid;
    }

    public async Task<SearchResultsViewModel> SearchDocumentsAsync(string? sortBy = null, int currentPage = 1, int currentPageSize = 5, string? esBoolQuery = null)
    {
        Console.WriteLine("ES Bool Query: " + esBoolQuery);

        if (!string.IsNullOrEmpty(esBoolQuery) && esBoolQuery != "{}")
        {
            var rawQuery = new RawQuery(esBoolQuery);

            // Detect fields used in query
            var usedFields = new HashSet<string>();
            using (JsonDocument doc = JsonDocument.Parse(esBoolQuery))
            {
                var mustClauses = doc.RootElement.GetProperty("bool").GetProperty("should");

                foreach (var clause in mustClauses.EnumerateArray())
                {
                    foreach (var property in clause.EnumerateObject())
                    {
                        var fieldName = property.Value.EnumerateObject().First().Name;
                        usedFields.Add(fieldName);
                    }
                }
            }

            Console.WriteLine("Used Fields: " + string.Join(", ", usedFields));

            // Prepare highlighting dynamically
            IHighlight highlightBuilder(HighlightDescriptor<DocumentViewModel> h)
            {
                h.PreTags("<mark>").PostTags("</mark>");

                return h.Fields(fs =>
                {
                    fs.Field("attachment.content")
                    //   .Field(usedFields.Contains("fileName.keyword") ? "fileName" : null)
                    //   .Field(usedFields.Contains("fileType.keyword") ? "fileType" : null)
                      .FragmentSize(200)
                      .NumberOfFragments(50)
                      .NoMatchSize(150);

                    // if (usedFields.Contains("fileName.keyword"))
                    // {
                    //     fs.Field("fileName")
                    //       .FragmentSize(200)
                    //       .NumberOfFragments(50)
                    //       .NoMatchSize(150);
                    // }
                    // if (usedFields.Contains("fileType.keyword"))
                    // {
                    //     fs.Field("fileType")
                    //       .FragmentSize(50)
                    //       .NumberOfFragments(1)
                    //       .NoMatchSize(20);
                    // }
                    
                    return fs;
                });
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
                .Index("documents")
                .Query(q => rawQuery)
            );

            // Search
            var response = await _elasticClient.SearchAsync<DocumentViewModel>(s => s
                .Index("documents")
                .Query(q => q.Bool(b => b.Must(rawQuery)))
                .Highlight(highlightBuilder)
                .Sort(sort)
                .Skip((currentPage - 1) * currentPageSize)
                .Take(currentPageSize)
            );

            var decoded = Encoding.UTF8.GetString(response.ApiCall.RequestBodyInBytes);
            Console.WriteLine("ElasticClient Response Decoded: " + decoded);

            // Process results
            var results = new List<GroupedSearchResults>();
            foreach (var hit in response.Hits)
            {
                // Console.WriteLine($"Document ID: {hit.Id}");

                // foreach (var highlight in hit.Highlight)
                // {
                //     Console.WriteLine($"Highlighted Field: {highlight.Key}");
                //     foreach (var fragment in highlight.Value)
                //     {
                //         Console.WriteLine($" - {fragment}");
                //     }
                // }

                var snippets = new List<string>();
                var highlightedFileNames = new List<string>();
                var highlightedFileTypes = new List<string>();
                var highlightedUploadedDates = new List<string>();

                if (hit.Highlight.TryGetValue("attachment.content", out var contentHighlights))
                    snippets.AddRange(contentHighlights);

                if (hit.Highlight.TryGetValue("fileName", out var fileNameHighlights))
                    highlightedFileNames.AddRange(fileNameHighlights);

                if (hit.Highlight.TryGetValue("fileType", out var fileTypeHighlights))
                    highlightedFileTypes.AddRange(fileTypeHighlights);

                results.Add(new GroupedSearchResults
                {
                    Id = hit.Id,
                    FileName = hit.Source.FileName,
                    UploadedDate = hit.Source.UploadedDate,
                    Snippets = snippets,
                    HighlightedFileName = highlightedFileNames.Count != 0 ? "<mark>" + highlightedFileNames.FirstOrDefault() + "</mark>" : null,
                    HighlightedFileType = highlightedFileTypes.Count != 0 ? "<mark>" + highlightedFileTypes.FirstOrDefault() + "</mark>" : null,
                    HighlightedUploadedDate = usedFields.Contains("uploadedDate")
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

    public async Task<DisplayFilesViewModel> GetFilesAsync(PaginationViewModel paginationViewModel)
    {
        var searchResponse = await _elasticClient.SearchAsync<DocumentViewModel>(s => s
            .Index("documents")
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
                .Index("documents")
                .Query(q => q.Wildcard(w => w
                    .Field(f => f.FileName.Suffix("keyword"))
                    .Value($"*{paginationViewModel.SearchString.ToLowerInvariant()}*")
                ))
            )).Count;

        DisplayFilesViewModel displayFilesViewModel = new()
        {
            Pagination = paginationViewModel,
            Files = files
        };

        return displayFilesViewModel;
    }

    public async Task<List<string>?> GetSynonymsAsync(string word)
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

    public async Task<List<string>> GetAllFileIdsAsync()
    {
        var searchResponse = await _elasticClient.SearchAsync<FileViewModel>(s => s
            .Index("documents")
            .Size(10000)  // Elasticsearch default limit is 10,000
            .Source(src => src.Includes(f => f.Field(fm => fm.Id)))
            .Query(q => q.MatchAll())
        );

        return searchResponse.Documents.Select(d => d.Id).ToList();
    }
}
