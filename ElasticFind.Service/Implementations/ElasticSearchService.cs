using System.Text;
using System.Text.Json;
using ElasticFind.Repository.ViewModels;
using ElasticFind.Service.Interfaces;
using Elasticsearch.Net;
using Microsoft.Extensions.Caching.Memory;
using Nest;
using Newtonsoft.Json.Linq;
using System;

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
        var response = await _elasticClient.DeleteAsync<DocumentViewModel>(id, d => d.Index("documents_v2").Refresh(Refresh.WaitFor));
        Console.WriteLine($"Delete response: {response.DebugInformation}");
        return response.IsValid;
    }

    public async Task<bool> DeleteMultipleFilesAsync(string id)
    {
        var response = await _elasticClient.DeleteAsync<DocumentViewModel>(id, d => d.Index("documents_v2"));
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
            using var doc = JsonDocument.Parse(esBoolQuery);
            CollectFields(doc.RootElement, usedFields);
            Console.WriteLine("Used Fields: " + string.Join(", ", usedFields));

            // Prepare highlighting dynamically
            IHighlight highlightBuilder(HighlightDescriptor<DocumentViewModel> h)
            {
                // h.PreTags("<mark>").PostTags("</mark>");

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

                if (usedFields.Contains("fileName.keyword"))
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

                if (usedFields.Contains("fileType.keyword"))
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

                if (usedFields.Contains("uploadedDate"))
                {
                    Console.WriteLine("Highlighting uploadedDate field");
                    highlightFields.Add(f => f
                        .Field("uploadedDate.text")
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
                .Index("documents_v2")
                .Query(q => rawQuery)
            );

            // Search
            var response = await _elasticClient.SearchAsync<DocumentViewModel>(s => s
                .Index("documents_v2")
                .Query(q => rawQuery)
                .Highlight(highlightBuilder)
                .Sort(sort)
                .Skip((currentPage - 1) * currentPageSize)
                .Take(currentPageSize)
            );

            var decodedRequest = Encoding.UTF8.GetString(response.ApiCall.RequestBodyInBytes);
            Console.WriteLine("ElasticClient Request Decoded: " + decodedRequest);


            // Process results
            var results = new List<GroupedSearchResults>();
            foreach (var hit in response.Hits)
            {
                Console.WriteLine($"Document ID: {hit.Id}");

                foreach (var highlight1 in hit.Highlight)
                {
                    Console.WriteLine($"Highlighted Field: {highlight1.Key}");
                    // foreach (var fragment in highlight1.Value)
                    // {
                    //     Console.WriteLine($" - {fragment}");
                    // }
                }

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

                if (hit.Highlight.TryGetValue("uploadedDate.text", out var uploadedDateHighlights))
                {
                    Console.WriteLine("Uploaded Date Highlights Found");
                    highlightedUploadedDate = uploadedDateHighlights.FirstOrDefault()?.Split('T')[0];
                }

                var fileNameParts = hit.Source.FileName.LastIndexOf('.') is int lastDotIndex && lastDotIndex > 0
                    ? new
                    {
                        Name = hit.Source.FileName.Substring(0, lastDotIndex),
                        Extension = string.Concat(".", hit.Source.FileName.AsSpan(lastDotIndex + 1))
                    }
                    : new { Name = hit.Source.FileName, Extension = string.Empty };

                Console.WriteLine("Content Highlights: " + snippets[0]);
                Console.WriteLine("File Name Highlights: " + string.Join(", ", highlightedFileNames));
                Console.WriteLine("File Type Highlights: " + string.Join(", ", highlightedFileTypes));
                Console.WriteLine("Uploaded Date Highlights: " + highlightedUploadedDate);

                results.Add(new GroupedSearchResults
                {
                    Id = hit.Id,
                    FileName = hit.Source.FileName,
                    UploadedDate = hit.Source.UploadedDate,
                    Snippets = snippets,
                    // HighlightedFileName = highlightedFileNames.Count != 0 && highlightedFileTypes.Count != 0 ? "<mark>" + hit.Source.FileName + "</mark>" : highlightedFileNames.Count != 0 ? "<mark>" + fileNameParts.Name + "</mark>" + fileNameParts.Extension : highlightedFileTypes.Count != 0 ? fileNameParts.Name + "<mark>" + fileNameParts.Extension + "</mark>" : hit.Source.FileName,
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

    public async Task<DisplayFilesViewModel> GetFilesAsync(PaginationViewModel paginationViewModel)
    {
        var searchResponse = await _elasticClient.SearchAsync<DocumentViewModel>(s => s
            .Index("documents_v2")
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
                .Index("documents_v2")
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
            .Index("documents_v2")
            .Size(10000)  // Elasticsearch default limit is 10,000
            .Source(src => src.Includes(f => f.Field(fm => fm.Id)))
            .Query(q => q.MatchAll())
        );

        return searchResponse.Documents.Select(d => d.Id).ToList();
    }
}
