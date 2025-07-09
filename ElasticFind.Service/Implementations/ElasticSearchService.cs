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

    public async Task<SearchResultsViewModel> SearchDocumentsAsync(
    string searchType, bool matchAllTerms, string keyword, string? fileTypeFilter = null, DateTime? startDate = null,
    DateTime? endDate = null, string? sortBy = null, string? searchInput = null, int currentPage = 1, int currentPageSize = 5, string? esBoolQuery = null)
    {
        Console.WriteLine("ES Bool Query: " + esBoolQuery);
        if (!string.IsNullOrEmpty(esBoolQuery) && esBoolQuery != "{}")
        {
            var rawQuery = new RawQuery(esBoolQuery);

            var countResponse1 = await _elasticClient.CountAsync<DocumentViewModel>(c => c
            .Index("documents")
            .Query(q => rawQuery)
            );
            Console.WriteLine("Total documents matching criteria: " + countResponse1.Count);

            var response1 = await _elasticClient.SearchAsync<DocumentViewModel>(s => s
                .Index("documents")
                .Query(q => q.Bool(b => b.Must(rawQuery)))
                .Highlight(h => h
                    .Fields(f => f
                        .Field("attachment.content")
                        .PreTags("<mark>")
                        .PostTags("</mark>")
                        .FragmentSize(200)
                        .NumberOfFragments(50)
                        .NoMatchSize(150)
                    )
                )
                .Skip((currentPage - 1) * currentPageSize)
                .Take(currentPageSize)
            );

            var decoded1 = Encoding.UTF8.GetString(response1.ApiCall.RequestBodyInBytes);
            Console.WriteLine("ElasticClient Response Decoded: " + decoded1);

            var results1 = new List<GroupedSearchResults>();

            foreach (var hit in response1.Hits)
            {
                if (hit.Highlight.TryGetValue("attachment.content", out var highlights))
                {
                    results1.Add(new GroupedSearchResults
                    {
                        Id = hit.Id,
                        FileName = hit.Source.FileName,
                        UploadedDate = hit.Source.UploadedDate,
                        Snippets = highlights.ToList()
                    });
                }
            }

            SearchResultsViewModel searchResults1 = new()
            {
                TotalDocuments = (int)countResponse1.Count,
                SearchResults = results1,
            };

            return searchResults1;
        }
        if (string.IsNullOrEmpty(keyword) || string.IsNullOrWhiteSpace(keyword))
        {
            Console.WriteLine("Error: Keyword is null or empty, returning empty results.");
            return new SearchResultsViewModel();
        }

        var mustQueries = new List<Func<QueryContainerDescriptor<DocumentViewModel>, QueryContainer>>();

        if (searchType == "1")
        {
            // Contain like search
            if (keyword.Contains(' '))
            {
                mustQueries.Add(q => q.MatchPhrase(mp => mp
                    .Field(f => f.Attachment.Content)
                    .Query(keyword)
                ));
            }
            else
            {
                mustQueries.Add(q => q.Wildcard(w => w
                    .Field(f => f.Attachment.Content)
                    .Value($"*{keyword.ToLowerInvariant()}*")
                ));
            }
        }

        else if (searchType == "2")
        {
            // Full-text search on content (multiple text search)
            if (matchAllTerms)
            {
                mustQueries.Add(q => q.Match(m => m
                    .Field(f => f.Attachment.Content)
                    .Query(keyword)
                    .Operator(Operator.And) // Match all terms
                ));

                // var keywords = keyword.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                // foreach (var term in keywords)
                // {
                //     var fuzzinessValue = term.Length switch
                //     {
                //         <= 2 => 0,
                //         <= 5 => 1,
                //         <= 10 => 2,
                //         _ => 3,
                //     };

                //     mustQueries.Add(q => q.Match(m => m
                //         .Field(f => f.Attachment.Content)
                //         .Query(term)
                //         .Fuzziness(Fuzziness.EditDistance(fuzzinessValue))
                //         .Operator(Operator.And)
                //     ));
                // }
            }
            else
            {
                mustQueries.Add(q => q.Match(m => m
                    .Field(f => f.Attachment.Content)
                    .Query(keyword)
                ));

                // var keywords = keyword.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                // foreach (var term in keywords)
                // {
                //     var fuzzinessValue = term.Length switch
                //     {
                //         <= 2 => 0,
                //         <= 5 => 1,
                //         <= 10 => 2,
                //         _ => 3,
                //     };

                //     mustQueries.Add(q => q.Match(m => m
                //         .Field(f => f.Attachment.Content)
                //         .Query(term)
                //         .Fuzziness(Fuzziness.EditDistance(fuzzinessValue))
                //     ));
                // }
            }
        }
        else if (searchType == "3") // Fuzzy search
        {
            if (matchAllTerms)
            {
                mustQueries.Add(q => q.Match(fz => fz
                    .Field(f => f.Attachment.Content)
                    .Query(keyword)
                    .Fuzziness(Fuzziness.Auto)
                    .Operator(Operator.And) // Match all terms
                ));
            }
            else
            {
                mustQueries.Add(q => q.Match(fz => fz
                    .Field(f => f.Attachment.Content)
                    .Query(keyword)
                    .Fuzziness(Fuzziness.Auto)
                ));
            }

            // var keywords = keyword.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            // Console.WriteLine("Keywords: " + string.Join(", ", keywords));
            // Console.WriteLine("Keywords Count: " + keywords.Length);
            // foreach (var term in keywords)
            // {
            //     var fuzzinessValue = term.Length switch
            //     {
            //         <= 2 => 0,
            //         <= 5 => 1,
            //         <= 10 => 2,
            //         _ => 3,
            //     };

            //     mustQueries.Add(q => q.Fuzzy(m => m
            //         .Field(f => f.Attachment.Content)
            //         .Value(term)
            //         .Fuzziness(Fuzziness.EditDistance(fuzzinessValue))
            //     ));
            // }
        }
        else    // Synonym search
        {
            var terms = keyword.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            var allTermsWithSynonyms = new List<string>(terms);

            foreach (var term in terms)
            {
                var synonyms = await GetSynonymsAsync(term);
                allTermsWithSynonyms.AddRange(synonyms);
            }
            foreach (var term in allTermsWithSynonyms)
            {
                Console.WriteLine("Term with Synonym: " + term);
            }

            mustQueries.Add(q => q.Bool(b => b
                .Should(allTermsWithSynonyms.Select(t => (Func<QueryContainerDescriptor<DocumentViewModel>, QueryContainer>)(m =>
                    m.Match(mm => mm
                        .Field(f => f.Attachment.Content)
                        .Query(t)
                    ))).ToArray())
                .MinimumShouldMatch(1)
            ));
        }

        // Filter by file type
        if (!string.IsNullOrWhiteSpace(fileTypeFilter) && fileTypeFilter != "File Type" && fileTypeFilter != "Other")
        {
            if (fileTypeFilter.Contains('/'))
            {
                // Multiple file types selected, split by '/'
                var extensions = fileTypeFilter.Split('/', StringSplitOptions.RemoveEmptyEntries)
                                               .Select(ext => ext.Trim())
                                               .ToList();

                mustQueries.Add(q => q.Terms(t => t
                    .Field(f => f.FileType.Suffix("keyword"))
                    .Terms(extensions)
                ));
            }
            else
            {
                // Single file type
                mustQueries.Add(q => q.Term(t => t
                    .Field(f => f.FileType.Suffix("keyword"))
                    .Value(fileTypeFilter.Trim())
                ));
            }
        }
        else if (fileTypeFilter == "Other")
        {
            var excludedFileTypes = new[] { ".pdf", ".docx", ".xlsx", ".xls", ".doc", ".txt", ".pptx", ".ppt", ".rtf" };

            mustQueries.Add(q => q.Bool(b => b
                .MustNot(excludedFileTypes.Select(fileType => (Func<QueryContainerDescriptor<DocumentViewModel>, QueryContainer>)(mn =>
                    mn.Term(t => t
                        .Field(f => f.FileType.Suffix("keyword"))
                        .Value(fileType)
                    ))).ToArray())
            ));
        }

        if (!string.IsNullOrWhiteSpace(searchInput))
        {
            mustQueries.Add(q => q.Wildcard(w => w
                .Field(f => f.FileName.Suffix("keyword"))
                .Value($"*{searchInput.ToLowerInvariant()}*")
            ));
        }

        // Normalize to date only
        var start = startDate?.Date; //Sets time component to 00:00:00
        var end = endDate?.Date.AddDays(1).AddTicks(-1); // End of the day, Sets time component to 23:59:59
        // Filter by date range
        if (start.HasValue || end.HasValue)
        {
            mustQueries.Add(q => q.DateRange(dr => dr
                .Field(f => f.UploadedDate)
                .GreaterThanOrEquals(start)
                .LessThanOrEquals(end)
            ));
        }

        // Sorting
        Func<SortDescriptor<DocumentViewModel>, IPromise<IList<ISort>>>? sort = null;
        if (!string.IsNullOrWhiteSpace(sortBy))
        {
            sort = s =>
            {
                switch (sortBy)
                {
                    case "1": // Sort by date
                        s.Descending(f => f.UploadedDate);
                        break;
                    case "2": // Sort by file type
                        s.Ascending(f => f.FileType.Suffix("keyword"));
                        break;
                    case "3": // Sort by file name
                        s.Ascending(f => f.UploadedDate);
                        break;
                }
                return s;
            };
        }

        var countResponse = await _elasticClient.CountAsync<DocumentViewModel>(c => c
            .Index("documents")
            .Query(q => q.Bool(b => b.Must(mustQueries)))
        );
        Console.WriteLine("Total documents matching criteria: " + countResponse.Count);

        var countResponse2 = await _elasticClient.CountAsync<DocumentViewModel>(c => c
            .Index("documents")
            .Query(q => q.Bool(b => b.Must(mustQueries)))
            .RequestConfiguration(r => r
                .DisableDirectStreaming()
            )
        );

        string request = countResponse2.DebugInformation;
        Console.WriteLine("Request Info: ");
        Console.WriteLine(request);

        var response = await _elasticClient.SearchAsync<DocumentViewModel>(s => s
            .Index("documents")
            .Query(q => q.Bool(b => b.Must(mustQueries)))
            .Highlight(h => h
            .Fields(
                f => f
                    .Field("attachment.content")
                    .PreTags("<mark>")
                    .PostTags("</mark>")
                    .FragmentSize(200)
                    .NumberOfFragments(50)
                    .NoMatchSize(150),
                f => f
                    .Field("fileName")
                    .PreTags("<mark>")
                    .PostTags("</mark>")
                    .FragmentSize(100)
                    .NumberOfFragments(50)
                    .NoMatchSize(150),
                f => f
                    .Field("fileType")
                    .PreTags("<mark>")
                    .PostTags("</mark>")
                    .FragmentSize(100)
                    .NumberOfFragments(50)
                    .NoMatchSize(150),
                f => f
                    .Field("uploadedDate")
                    .PreTags("<mark>")
                    .PostTags("</mark>")
                    .FragmentSize(100)
                    .NumberOfFragments(50)
                    .NoMatchSize(150)
            )
)
            .Sort(sort)
            .Skip((currentPage - 1) * currentPageSize)
            .Take(currentPageSize)
        ); 

        var decoded = Encoding.UTF8.GetString(response.ApiCall.RequestBodyInBytes);
        Console.WriteLine("ElasticClient Response Decoded: " + decoded);

        var results = new List<GroupedSearchResults>();

        foreach (var hit in response.Hits)
        {
            if (hit.Highlight.TryGetValue("attachment.content", out var highlights))
            {
                results.Add(new GroupedSearchResults
                {
                    Id = hit.Id,
                    FileName = hit.Source.FileName,
                    UploadedDate = hit.Source.UploadedDate,
                    Snippets = highlights.ToList()
                });
            }
        }

        SearchResultsViewModel searchResults = new()
        {
            TotalDocuments = (int)countResponse.Count,
            SearchResults = results,
        };

        return searchResults;
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

    public QueryContainer ConvertRulesToElasticsearchQuery(QueryBuilderRule rules)
    {
        throw new NotImplementedException();
    }

    // public async Task<SearchResultsViewModel> QueryBuilderSearch(QueryBuilderRule rules)
    // {
    //     QueryContainer query = new();

    //     if (rules == null || rules.Rules == null)
    //         return new SearchResultsViewModel();

    //     foreach (var rule in rules.Rules)
    //     {
    //         if (rule.Value == null || string.IsNullOrEmpty(rule.Operator))
    //             continue;

    //         var field = rule.Field;
    //         var op = rule.Operator;
    //         var val = rule.Value;

    //         switch (op)
    //         {
    //             case "equal":
    //                 query &= new TermQuery { Field = field, Value = val };
    //                 break;

    //             case "not_equal":
    //                 query &= !new TermQuery { Field = field, Value = val };
    //                 break;

    //             case "in":
    //                 query &= new TermsQuery { Field = field, Terms = ((IEnumerable<object>)val).Select(v => v.ToString()) };
    //                 break;

    //             case "not_in":
    //                 query &= !new TermsQuery { Field = field, Terms = ((IEnumerable<object>)val).Select(v => v.ToString()) };
    //                 break;

    //             case "less":
    //                 query &= new RangeQuery { Field = field, LessThan = val };
    //                 break;

    //             case "less_or_equal":
    //                 if (val is DateTime || DateTime.TryParse(val.ToString(), out _))
    //                 {
    //                     query = new DateRangeQuery
    //                     {
    //                         Field = field,
    //                         GreaterThanOrEqualTo = (DateMath)val,
    //                         LessThanOrEqualTo = (DateMath)val // example – adjust as needed
    //                     };
    //                 }
    //                 else if (double.TryParse(val.ToString(), out _))
    //                 {
    //                     query = new NumericRangeQuery
    //                     {
    //                         Field = field,
    //                         GreaterThanOrEqualTo = (double?)val,
    //                         LessThanOrEqualTo = (double?)val // example – adjust as needed
    //                     };
    //                 }
    //                 break;

    //             case "greater":
    //                 query &= new RangeQuery { Field = field, GreaterThan = val };
    //                 break;

    //             case "greater_or_equal":
    //                 query &= new RangeQuery { Field = field, GreaterThanOrEqualTo = val };
    //                 break;

    //             case "between":
    //                 var rangeValues = ((JArray)val).ToObject<List<object>>();
    //                 if (rangeValues.Count == 2)
    //                 {
    //                     query &= new RangeQuery
    //                     {
    //                         Field = field,
    //                         GreaterThanOrEqualTo = rangeValues[0],
    //                         LessThanOrEqualTo = rangeValues[1]
    //                     };
    //                 }
    //                 break;

    //             case "not_between":
    //                 var notRange = ((JArray)val).ToObject<List<object>>();
    //                 if (notRange.Count == 2)
    //                 {
    //                     query &= !new RangeQuery
    //                     {
    //                         Field = field,
    //                         GreaterThanOrEqualTo = notRange[0],
    //                         LessThanOrEqualTo = notRange[1]
    //                     };
    //                 }
    //                 break;

    //             case "contains":
    //                 query &= new MatchQuery { Field = field, Query = val.ToString() };
    //                 break;
    //         }
    //     }
    // }
}
